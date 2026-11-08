namespace OpenPhantasie.Core;

/// <summary>What the outdoor loop is waiting for (<see cref="P4OutdoorLoop.Prompt"/>).</summary>
public enum P4OutdoorPrompt
{
    /// <summary>The next player action: a move (<see cref="P4OutdoorLoop.Move"/>) or a command that takes no step.</summary>
    Action,

    /// <summary>"SPACE": the message window waits for a key (<c>_spc_wait</c>; <see cref="P4OutdoorLoop.Continue"/>).</summary>
    Wait,

    /// <summary>A yes / no question (<c>_confirm</c>; <see cref="P4OutdoorLoop.Answer"/>).</summary>
    Confirm,

    /// <summary>A fight (<see cref="P4OutdoorLoop.PendingFight"/>) for the combat loop; then <see cref="P4OutdoorLoop.FightOver"/>.</summary>
    Fight,

    /// <summary>The party went into a town or a location map (<see cref="P4OutdoorLoop.Transfer"/>).</summary>
    Left,

    /// <summary>Nobody is alive: "The party has been wiped out." (the game ends).</summary>
    PartyLost,
}

/// <summary>How a fight ended, as the loops after it need it.</summary>
public enum P4FightOutcome
{
    Won,

    /// <summary>Fled, begged, teleported away or any other end the party survived without winning.</summary>
    NotWon,

    Lost,
}

/// <summary>
/// An outdoor fight (<c>_oencgo(einf1, einf2)</c>): the party surprised the monsters (<c>_einf1</c>), the monsters
/// surprised the party (<c>_einf2</c>), whether it is fought at sea (water, rapids or shining sea under the party: the
/// ship battlefield and the pirates, <c>outdoor.md</c> §4.2), a night attack, and the map's area level <c>_al</c>.
/// Which monster group <c>_encount</c> picks outdoors is not decoded <c>[?]</c> (<c>combat.md</c>), so none is named here.
/// </summary>
public sealed record P4OutdoorFight(bool PartySurprises, bool MonstersSurprise, bool AtSea, bool NightAttack, int AreaLevel);

/// <summary>
/// Phantasie IV's outdoor loop (<c>_outdoor</c> → <c>_oload</c> → <c>_omain</c>; spec
/// <c>reverse-engineering/x68000/phantasie4/logic-specs/outdoor.md</c>): eight-way moves on the 26 × 26 <c>MOUT</c> maps,
/// map-edge crossings, blocked terrain, time, swimming, lava, ships (boarding, drift, storms, running aground), the
/// shining sea, towns, inns, dungeons, pools, Charon and the encounter roll with its night watch and surprise. Headless and
/// resumable like <see cref="P4Dungeon"/>: every key the original waits for is a <see cref="Prompt"/>, and
/// <see cref="Capture"/> saves the whole state, mid-question included.
/// <para>
/// Every arrival on a map (<see cref="Arrive"/>: a dungeon or town exit, an edge crossing, a pool, Charon, an inn stay, the
/// end of a fight, a loaded game) runs a full step on the arrival square, as <c>_omain</c> does on entry. Directions are
/// <c>_omove</c>'s 0–7: 0 SW, 1 S, 2 SE, 3 W, 4 E, 5 NW, 6 N, 7 NE (tables <c>0x42EF2</c> / <c>0x42F12</c>).
/// </para>
/// </summary>
public sealed class P4OutdoorLoop
{
    /// <summary><c>_omove</c>'s (dx, dy) per direction 0–7; dy is north-positive (the new y is y − dy) [C].</summary>
    private static readonly int[] Dx = [-1, 0, 1, -1, 1, -1, 0, 1], Dy = [-1, -1, -1, 0, 0, 1, 1, 1];

    /// <summary>Terrain types with a handler of their own (<c>_omain</c>'s jump table) [C].</summary>
    public const int Water = 0, Rapids = 1, Lava = 7, ShiningSea = 13, TownTile = 14, InnTile = 15, DungeonTile = 16,
        PoolTile = 17, CharonTile = 18;

    /// <summary>Story flag <c>_par</c>+15C (= flag 0x3C): the shining sea no longer pushes back and counts as water [C].</summary>
    public const int BarrierFlag = 0x3C;

    /// <summary>The outdoor inn: party gold ≥ 256, pays 256 and passes 1000 hours (<c>_oinn</c>; <c>rule-tables.json</c>
    /// <c>towns.outdoor_inn_cost</c> / <c>outdoor_inn_hours</c>) [C].</summary>
    public int InnCost => _tables.Services?.OutdoorInnCost ?? 256;

    public int InnHours => _tables.Services?.OutdoorInnHours ?? 1000;

    /// <summary>Charon on outdoor map 17 goes to outdoor map 21 (3, 2) (<c>_oboatman</c>) [C].</summary>
    public const int StyxPortMap = 17;

    private readonly GameOptions _options;
    private readonly P4Tables _tables;
    private readonly P4Atlas _atlas;
    private readonly IRandomSource _rng;
    private readonly P4MessageWindow _window = new();

    public P4OutdoorLoop(GameOptions options, P4Tables tables, P4Atlas atlas, P4Party party, IRandomSource rng)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        Party = party ?? throw new ArgumentNullException(nameof(party));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public P4Party Party { get; }

    private P4OutdoorTables T => _tables.Outdoor;

    /// <summary><c>_cmapn</c>, <c>_ccx</c> / <c>_ccy</c>.</summary>
    public int Map { get; private set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    /// <summary><c>_cox</c> / <c>_coy</c>: the square before the last move (where a ship left behind is moored).</summary>
    public int OldX { get; private set; }

    public int OldY { get; private set; }

    /// <summary>The loaded map, or null before the first arrival.</summary>
    public P4OutdoorMap? MapData => Map == 0 ? null : _atlas.Outdoors[Map];

    /// <summary>The drift counter (<c>0x43004</c>): forced moves still to come while at sea.</summary>
    public int Drift { get; private set; }

    public P4OutdoorPrompt Prompt { get; private set; } = P4OutdoorPrompt.Action;

    /// <summary>The message window.</summary>
    public IReadOnlyList<string> Box => _window.Lines;

    public P4OutdoorFight? PendingFight { get; private set; }

    /// <summary>Where the party went (<see cref="P4OutdoorPrompt.Left"/>): a town (square 0, 0) or a location map's entry.</summary>
    public P4Transfer? Transfer { get; private set; }

    /// <summary>Terrain type of the party's square (tile / 10).</summary>
    public int TerrainType => P4OutdoorMap.TypeOf(MapData!.Tile(X, Y));

    // --- Loop state ---------------------------------------------------------------------------------------------------

    private enum Stage
    {
        Idle, PassTop, Tile, AfterHandler, Encounter, AfterPass, InnStay, Barrier, FightAfterWait, Stopped,
    }

    /// <summary>The open question (<see cref="P4OutdoorPrompt.Confirm"/>).</summary>
    private enum Ask { None, Town, Inn, Dungeon, Pool, Charon, Barrier, SleepingMonsters, Fight }

    private Stage _stage = Stage.Idle;
    private Ask _ask;

    /// <summary>The terrain type whose name was printed last (−1 on each arrival).</summary>
    private int _lastType = -1;

    /// <summary><c>_watf</c> / the step's encounter check / the sub-index of the tile under the party.</summary>
    private bool _watf, _check;

    /// <summary><c>_einf3</c>: a fight not started by attacking sleeping monsters waits for a key first.</summary>
    private bool _einf3;

    private P4OutdoorFight? _fightAfterWait;

    /// <summary>The move under way (for a forced move's tick after the shining-sea question).</summary>
    private bool _forced;

    // --- Entry and input ----------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_oload</c> + <c>_omain</c>'s entry: the party stands on (x, y) of outdoor map <paramref name="map"/> (the old
    /// square too), a ship the party is aboard follows, the drift counter is cleared, and the arrival square's step runs.
    /// </summary>
    public void Arrive(int map, int x, int y)
    {
        if (!_atlas.Outdoors.ContainsKey(map))
            throw new ArgumentOutOfRangeException(nameof(map), $"No outdoor map {map}.");
        Transfer = null;
        PendingFight = null;
        Prompt = P4OutdoorPrompt.Action;
        Load(map, x, y);
        Run();
    }

    private void Load(int map, int x, int y, bool jump = false)
    {
        OverviewBlock = T.Overview.BlockFor(map, OverviewBlock, jump);
        if (Party.Ship == P4ShipState.Aboard)
            (Party.ShipMap, Party.ShipX, Party.ShipY) = (map, x, y);
        (Map, X, Y, OldX, OldY) = (map, x, y, x, y);
        Drift = 0;
        _lastType = -1;
        _stage = Stage.PassTop;
    }

    /// <summary>A move in direction <paramref name="dir"/> (0–7, see the class notes).</summary>
    public void Move(int dir)
    {
        Require(P4OutdoorPrompt.Action);
        if (dir is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(dir));
        _window.Clear();
        StartMove(dir, forced: false);
        Run();
    }

    /// <summary>
    /// M: the overview / close-up toggle. By the manual only the view changes (D4-057); with <c>p4.map-step</c> =
    /// <c>x68000</c> it also runs the square's step again (time, the tile, the encounter roll; <c>_omain</c> <c>0x15E2A</c>) [C].
    /// </summary>
    public void ToggleMap()
    {
        Require(P4OutdoorPrompt.Action);
        MapOverview = !MapOverview;
        if (!_options.P4Code(P4RuleOptions.MapStep))
            return;
        _stage = Stage.PassTop;
        Run();
    }

    /// <summary>The overview (3 × 3 maps) or close-up view: <c>0x43002</c>, display only.</summary>
    public bool MapOverview { get; private set; }

    /// <summary>
    /// <c>_cblock</c>: the overview's block (<see cref="P4OverviewTable"/>), −1 before the first arrival. A walk across a map
    /// edge keeps it while the new map is in it; a jump (pool, Charon, Teleportation) takes the map's own block [C].
    /// </summary>
    public int OverviewBlock { get; private set; } = -1;

    /// <summary>
    /// The nine maps the overview draws, row-major (<c>_omap_load</c>): the block's maps; in the Netherworld only the party's
    /// own map, the other cells the hidden map (16) [C]. With <c>p4.overview</c> = <c>centred</c> (D4-072) the nine maps
    /// around the party's map instead, −1 = past the world's edge (drawn black); the Netherworld hiding stays.
    /// </summary>
    public IReadOnlyList<int> OverviewMaps
    {
        get
        {
            var o = T.Overview;
            var cells = _options.Is(P4RuleOptions.Overview, "centred")
                ? o.CentredOn(Map)
                : o.Blocks[Math.Clamp(OverviewBlock, 0, o.Blocks.Count - 1)];
            return Map >= o.NetherworldFrom ? [.. cells.Select(m => m == Map || m < 0 ? m : o.HiddenMap)] : cells;
        }
    }

    /// <summary><c>_oblkofs</c>: the party's map's cell in <see cref="OverviewMaps"/> (0–8), or −1.</summary>
    public int OverviewCell => OverviewCellOf(Map);

    /// <summary>
    /// Map <paramref name="map"/>'s cell in the overview (0–8), or −1 when the overview lacks it or its picture is hidden
    /// (the Netherworld shows only the party's own map).
    /// </summary>
    public int OverviewCellOf(int map)
    {
        var o = T.Overview;
        if (map < 0 || (Map >= o.NetherworldFrom && map != Map))
            return -1;
        return OverviewMaps.ToList().IndexOf(map);
    }

    /// <summary>"SPACE".</summary>
    public void Continue()
    {
        Require(P4OutdoorPrompt.Wait);
        Prompt = P4OutdoorPrompt.Action;
        Run();
    }

    /// <summary>The answer to <see cref="P4OutdoorPrompt.Confirm"/>.</summary>
    public void Answer(bool yes)
    {
        Require(P4OutdoorPrompt.Confirm);
        Prompt = P4OutdoorPrompt.Action;
        var ask = _ask;
        _ask = Ask.None;
        Answered(ask, yes);
        Run();
    }

    /// <summary>
    /// The fight is over (<c>_oencgo</c> after <c>_encount</c>). A lost party ends the game. Otherwise Levitation ends (the
    /// end of every fight clears it), a fight at sea leaves a ship moored on the party's square (<c>_shipf</c>) unless D4-038
    /// (manual) wrecks the party's own ship, and the party is reloaded on the same square: its step runs again.
    /// <paramref name="move"/> (<see cref="P4FightReturn"/>): after a flight, Beg or Teleportation <c>_enc_end</c> puts the
    /// party back on the old square first, and the ship is moored there (<see cref="P4FightMove.MoorHere"/>).
    /// </summary>
    public void FightOver(P4FightOutcome outcome, P4FightMove? move = null)
    {
        Require(P4OutdoorPrompt.Fight);
        var fight = PendingFight!;
        PendingFight = null;
        Prompt = P4OutdoorPrompt.Action;
        if (outcome == P4FightOutcome.Lost || !Party.AnyAlive)
        {
            Lose();
            return;
        }
        Party.Levitating = false;
        Party.LevitationSteps = null;
        _window.Clear();
        if (move is { MoorHere: true })
            (X, Y) = (move.X, move.Y);
        if (fight.AtSea)
        {
            if (Party.Ship != P4ShipState.None
                && P4Outdoor.ShipWreckedInFight(_options, _tables.Amounts, _rng, outcome == P4FightOutcome.Won))
            {
                Party.LoseShip();
                _window.Line(GameText.S("core.p4.ship_wrecked"));
            }
            else
                (Party.Ship, Party.ShipNavigator, Party.ShipMap, Party.ShipX, Party.ShipY) = (P4ShipState.Moored, 0, Map, X, Y);
        }
        if (move is not null)
            (X, Y) = (move.X, move.Y);
        Load(Map, X, Y);
        Run();
    }

    private void Require(P4OutdoorPrompt prompt)
    {
        if (Prompt != prompt)
            throw new InvalidOperationException($"The outdoor loop is waiting for {Prompt}, not {prompt}.");
    }

    private void WaitKey() => Prompt = P4OutdoorPrompt.Wait;

    private void Confirm(Ask ask)
    {
        _ask = ask;
        Prompt = P4OutdoorPrompt.Confirm;
    }

    // --- The loop -----------------------------------------------------------------------------------------------------

    private void Run()
    {
        while (Prompt == P4OutdoorPrompt.Action)
        {
            switch (_stage)
            {
                case Stage.PassTop: PassTop(); break;
                case Stage.Tile: Tile(); break;
                case Stage.AfterHandler: AfterHandler(); break;
                case Stage.Encounter: EncounterRoll(); break;
                case Stage.AfterPass: AfterPass(); break;
                case Stage.InnStay: InnStay(); break;
                case Stage.Barrier: Confirm(Ask.Barrier); _window.Print(GameText.S("p4x.0x430af")); break;
                case Stage.FightAfterWait:
                    PendingFight = _fightAfterWait;
                    _fightAfterWait = null;
                    _stage = Stage.Idle;
                    Prompt = P4OutdoorPrompt.Fight;
                    break;
                default: return;
            }
        }
    }

    /// <summary>
    /// Top of the step (<c>_omark(1)</c> → <c>_oship_mark</c>): on the moored ship's square the party boards it — the member
    /// with the highest Navigation takes the helm (first on a tie, condition not read) and the drift counter is cleared; a
    /// ship the party is aboard follows the party.
    /// </summary>
    private void PassTop()
    {
        _stage = Stage.Tile;
        Party.Sounds.Note(P4Sounds.OutdoorStep); // _omain 0x15F1C: _sdwlk after _omark(1), every pass top
        if (Party.ShipMap != Map)
            return;
        if (Party.Ship == P4ShipState.Moored && Party.ShipX == X && Party.ShipY == Y)
        {
            var nav = P4Outdoor.Navigator(Party.Members.Select(m => m.Skill(P4Skill.Navigation)).ToList()) ?? 0;
            _window.Print(GameText.S("p4x.0x43139"));
            _window.Print(P4MessageWindow.Format("p4x.0x4314e", Party.Members[nav].Name));
            Drift = 0;
            Party.Ship = P4ShipState.Aboard;
            Party.ShipNavigator = nav;
            WaitKey();
        }
        else if (Party.Ship == P4ShipState.Aboard)
            (Party.ShipX, Party.ShipY) = (X, Y);
    }

    /// <summary>The square's terrain: its name when it changes, the hours, then the tile's handler (<c>_omain</c>).</summary>
    private void Tile()
    {
        var tile = MapData!.Tile(X, Y);
        var type = P4OutdoorMap.TypeOf(tile);
        var sub = P4OutdoorMap.SubOf(tile);
        if (type != _lastType && Drift <= 1)
        {
            if (T.TerrainNameId(type) is { } name)
                _window.Print("\n\n" + GameText.S(name));
            _lastType = type;
        }
        Party.PassHours(T.HoursByType[type]);
        _watf = false;
        _check = false;
        if (type == ShiningSea && Party.StoryFlags[BarrierFlag] != 0)
            type = Water;
        _stage = Stage.AfterHandler;
        switch (type)
        {
            case Water or Rapids:
                WaterStep();
                _watf = true;
                _check = true;
                break;
            case Lava:
                LavaStep();
                break;
            case TownTile:
                var town = _atlas.Towns[MapData.Towns[sub]];
                // "\n%s" + "の町にきた。" for maps below 13: the English line puts the name after "the town of".
                _window.Line(town.Id < 13 ? GameText.S("p4x.0x43279") + " " + town.Name + "." : town.Name);
                _window.Print(GameText.S("p4x.0x43286"));
                Confirm(Ask.Town);
                break;
            case InnTile:
                var inn = MapData.Inns[sub];
                _window.Print(P4MessageWindow.Format("p4x.0x43228", GameText.S(_atlas.InnNameIds[inn])));
                if (Party.Gold >= InnCost)
                {
                    _window.Print(GameText.S("p4x.0x43237"));
                    Confirm(Ask.Inn);
                }
                break;
            case DungeonTile:
                var site = MapData.Dungeons[sub];
                _window.Print(P4MessageWindow.Format("p4x.0x43209", _atlas.DungeonNames[site.Location]));
                _window.Print(GameText.S("p4x.0x43216"));
                Confirm(Ask.Dungeon);
                break;
            case PoolTile:
                _window.Print(GameText.S("p4x.0x431bf"));
                Confirm(Ask.Pool);
                break;
            case CharonTile:
                _window.Print(P4MessageWindow.Format("p4x.0x43183", GameText.S(Map == StyxPortMap ? "p4x.0x4304d" : "p4x.0x43044")));
                _window.Print(GameText.S("p4x.0x4319b"));
                Confirm(Ask.Charon);
                break;
            default:
                _check = true;
                break;
        }
    }

    private void Answered(Ask ask, bool yes)
    {
        var m = MapData!;
        var sub = P4OutdoorMap.SubOf(m.Tile(X, Y));
        switch (ask)
        {
            case Ask.Town when yes:
                Transfer = new P4Transfer(P4Transfer.Town, m.Towns[sub], 0, 0, 0);
                break;
            case Ask.Dungeon when yes:
                var d = m.Dungeons[sub];
                Transfer = new P4Transfer(P4Transfer.Dungeon, d.Location, d.X, d.Y, 0);
                break;
            case Ask.Inn when yes:
                Party.Gold -= InnCost;
                _window.Print(GameText.S("p4x.0x4324b"));
                _window.Print(GameText.S("p4x.0x4325e"));
                _stage = Stage.InnStay;
                WaitKey();
                break;
            case Ask.Pool when yes:
                Transfer = m.Destination;
                _window.Print(GameText.S("p4x.0x431df"));
                break;
            case Ask.Charon when yes:
                Transfer = Map == StyxPortMap ? new P4Transfer(P4Transfer.Outdoors, 21, 3, 2, 0) : m.Destination;
                break;
            case Ask.Barrier:
                if (yes)
                {
                    Party.Sounds.Note(P4Sounds.ShipDestroyed); // _obarrier 0x14022: _sdnoi before the line
                    _window.Print(GameText.S("p4x.0x430c9"));
                    Party.LoseShip();
                }
                MoveDone(moved: false);
                break;
            case Ask.SleepingMonsters:
                if (yes)
                {
                    _einf3 = false;
                    StartFight(partySurprises: true, monstersSurprise: false, night: false);
                }
                break;
            case Ask.Fight:
                if (yes)
                {
                    if (P4Dice.Rand(_rng, 10) < 3)
                    {
                        _window.Print(GameText.S("p4x.0x4351b"));
                        StartFight(false, true, false);
                    }
                    else
                    {
                        _window.Print(GameText.S("p4x.0x43538"));
                        StartFight(true, false, false);
                    }
                }
                else if (P4Dice.Rand(_rng, 10) >= 8)
                {
                    _window.Print(GameText.S("p4x.0x43553"));
                    StartFight(false, false, false);
                }
                break;
        }
    }

    /// <summary>The outdoor inn stay after paying: <c>_inn</c>, 1000 hours, and the party reloaded on the same square.</summary>
    private void InnStay()
    {
        P4Town.InnStay(Party, _window, _tables.Services, _rng, _tables.Towns);
        Party.PassHours(InnHours);
        Transfer = new P4Transfer(P4Transfer.Outdoors, Map, X, Y, 0);
        _stage = Stage.AfterHandler;
    }

    /// <summary>
    /// <c>_owater</c>: aboard, with no forced move pending, the navigator may lose the course (D4-040: <see
    /// cref="P4Outdoor.Drifts"/>): rand(100) &lt; 15 "Caught in a storm!" and 10 forced moves, else 1. Not aboard, every
    /// living member with condition + <c>_pc</c> &lt; 4 or rand(100) &gt; Swim takes rand(5) + 1. D4-039 (manual): a
    /// levitating party does not swim.
    /// </summary>
    private void WaterStep()
    {
        if (Party.Ship == P4ShipState.Aboard)
        {
            if (Drift != 0)
                return;
            var nav = Party.Members[Party.ShipNavigator].Skill(P4Skill.Navigation);
            if (!P4Outdoor.Drifts(_options, T, _rng, nav))
                return;
            if (P4Dice.Rand(_rng, 100) < T.StormBelow)
            {
                _window.Print(GameText.S("p4x.0x43294"));
                Drift = T.StormMoves;
            }
            else
                Drift = 1;
            return;
        }
        if (Party.Levitating && P4Outdoor.LevitationWorksOutdoors(_options))
            return;
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            if (!m.Alive)
                continue;
            if ((m.Condition + m.CombatState < 4 || P4Dice.Rand(_rng, 100) > m.Skill(P4Skill.Swim))
                && Damage(i, P4Dice.Rand(_rng, T.SwimDie) + T.SwimAdd))
                return;
        }
    }

    /// <summary><c>_olava</c>: "It's hot!"; every living member with rand(18) ≤ Luck takes rand(5) (a luckier member more often) [C].</summary>
    private void LavaStep()
    {
        _window.Print(GameText.S("p4x.0x432a7"));
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            if (m.Alive && P4Dice.Rand(_rng, 18) <= m.Abilities[P4Member.Luck]
                && Damage(i, P4Dice.Rand(_rng, T.LavaDie)))
                return;
        }
    }

    /// <summary>
    /// <c>_pdamage</c> outside combat (type 0: no wound): at least 1; d &lt; HP → HP − d, else dead. "%s: %d damage!". True
    /// when nobody is left alive (the game ends).
    /// </summary>
    private bool Damage(int index, int d)
    {
        var m = Party.Members[index];
        Party.Sounds.Note(P4Sounds.MemberDamaged); // _pdamage 0x1E9C0: _sddam once d is at least 1
        d = Math.Max(1, d);
        if (d < m.Hp && m.Alive)
            m.Hp -= d;
        else
        {
            Party.Sounds.Note(P4Sounds.MemberKilled); // _pdamage 0x1EB4A
            m.Hp = 0;
            m.Condition = P4Member.Dead;
        }
        _window.Print(P4MessageWindow.Format("p4x.0x44113", m.Name, d));
        if (Party.AnyAlive)
            return false;
        Lose();
        return true;
    }

    private void Lose()
    {
        Party.Sounds.Note(P4Sounds.WipedOut); // _deadmsg 0x1E484
        _stage = Stage.Stopped;
        PendingFight = null;
        _window.Clear();
        _window.Line(GameText.S("p4x.0x44097"));
        _window.Line(GameText.S("p4x.0x440b0")); // s162: _deadmsg prints both lines wherever the party falls
        Prompt = P4OutdoorPrompt.PartyLost;
    }

    /// <summary>
    /// <c>_save_ship</c> on every square that is not water: a party aboard leaves the ship ("The party left the ship.", the
    /// ship moored on the square before), or, with forced moves still pending, "The ship ran aground!" and the ship is lost.
    /// </summary>
    private void AfterHandler()
    {
        _stage = Stage.Encounter;
        if (_watf || Party.Ship != P4ShipState.Aboard)
            return;
        if (Drift != 0)
        {
            Party.Sounds.Note(P4Sounds.RanAground); // _save_ship 0x14C8E: _sdnoi before the line
            _window.Print(GameText.S("p4x.0x4315f"));
            Party.LoseShip();
            Drift = 0;
        }
        else
        {
            _window.Print(GameText.S("p4x.0x4316e"));
            (Party.Ship, Party.ShipNavigator, Party.ShipMap, Party.ShipX, Party.ShipY) =
                (P4ShipState.Moored, 0, Map, OldX, OldY);
        }
        WaitKey();
    }

    /// <summary>The encounter roll: rand(1000) &lt; the terrain's per mille (types 13 and up read entry 0, 0) → <c>_oencchk</c>.</summary>
    private void EncounterRoll()
    {
        _stage = Stage.AfterPass;
        if (!_check)
            return;
        var type = P4OutdoorMap.TypeOf(MapData!.Tile(X, Y));
        if (type == ShiningSea && Party.StoryFlags[BarrierFlag] != 0)
            type = Water;
        var perMille = type < ShiningSea ? T.EncounterPerMilleByType[type] ?? 0 : 0;
        if (P4Dice.Rand(_rng, 1000) < perMille)
            EncounterCheck();
    }

    /// <summary>
    /// <c>_oencchk</c> (outdoor.md §5): a night attack (rand(100) &lt; 38), sleeping monsters (rand(100) &lt; 36), or the
    /// Listen roll and the approach.
    /// </summary>
    private void EncounterCheck()
    {
        var area = T.AreaLevelByMap[Map - 1];
        _einf3 = true;
        var n = Party.Members.Count;
        if (P4Dice.Rand(_rng, 100) < 38)
        {
            var found = false;
            var watch = 0;
            for (var tries = 0; tries < 15; tries++)
            {
                watch = P4Dice.Rand(_rng, n) - 1;
                if (Party.Members[watch].Alive)
                {
                    found = true;
                    break;
                }
            }
            var sleepers = new List<P4Member>();
            for (var i = 0; i < n; i++)
            {
                if (found && i == watch)
                    continue;
                if (Party.Members[i].Condition == P4Member.Okay)
                {
                    Party.Members[i].Condition = P4Member.Asleep;
                    sleepers.Add(Party.Members[i]);
                }
            }
            _window.Print(GameText.S("p4x.0x43420"));
            if (!found)
            {
                StartFight(false, true, night: true);
                return;
            }
            _window.Print(P4MessageWindow.Format("p4x.0x4343f", Party.Members[watch].Name));
            var heard = P4Dice.Rand(_rng, 100) < Party.Members[watch].Skill(P4Skill.Listen);
            if (!heard)
            {
                StartFight(false, true, night: true);
                return;
            }
            _window.Print(P4MessageWindow.Format("p4x.0x43462", Party.Members[watch].Name));
            if (P4Combat.WatchWakesParty(_options, heard))
            {
                // D4-048 manual: the watch wakes the whole party before the fight.
                foreach (var s in sleepers)
                    s.Condition = P4Member.Okay;
            }
            StartFight(false, false, night: true);
            return;
        }
        if (P4Dice.Rand(_rng, 100) < 36)
        {
            _window.Print(GameText.S("p4x.0x4347e"));
            _window.Print(GameText.S("p4x.0x434a0"));
            Confirm(Ask.SleepingMonsters);
            return;
        }
        var sensed = -1;
        for (var i = 0; i < n; i++)
        {
            if (P4Dice.Rand(_rng, 188) <= Party.Members[i].Skill(P4Skill.Listen) && Party.Members[i].Alive)
                sensed = i;
        }
        if (sensed >= 0)
            _window.Print(P4MessageWindow.Format("p4x.0x434b4", Party.Members[sensed].Name));
        if (P4Dice.Rand(_rng, 100) < 8 * area + 30)
        {
            if (sensed < 0 && P4Dice.Rand(_rng, 10) >= 5)
            {
                _window.Print(GameText.S("p4x.0x434f1"));
                StartFight(false, true, false);
            }
            else
            {
                _window.Print(GameText.S("p4x.0x434d5"));
                StartFight(false, false, false);
            }
            return;
        }
        if (sensed >= 0)
        {
            _window.Print(GameText.S("p4x.0x4350e"));
            Confirm(Ask.Fight);
            return;
        }
        _window.Print(GameText.S("p4x.0x4356f"));
        StartFight(false, false, false);
    }

    /// <summary><c>_oencgo</c>: the fight, after a key when <c>_einf3</c> is set (every fight but attacking sleepers).</summary>
    private void StartFight(bool partySurprises, bool monstersSurprise, bool night)
    {
        var raw = P4OutdoorMap.TypeOf(MapData!.Tile(X, Y));
        var fight = new P4OutdoorFight(partySurprises, monstersSurprise, raw is Water or Rapids or ShiningSea, night,
            T.AreaLevelByMap[Map - 1]);
        _stage = Stage.Idle;
        if (_einf3)
        {
            Party.Sounds.Note(P4Sounds.FightWait); // _oencgo 0x165F2: _sdbeep, then _spc_wait
            _fightAfterWait = fight;
            _stage = Stage.FightAfterWait;
            WaitKey();
            return;
        }
        PendingFight = fight;
        Prompt = P4OutdoorPrompt.Fight;
    }

    /// <summary>
    /// After the step: a move the step made (<c>_cmovf</c>) is carried out — to the outdoors here, to a town or location map
    /// through <see cref="P4OutdoorPrompt.Left"/>; else forced moves run while the drift counter is set; else the loop
    /// waits for the player.
    /// </summary>
    private void AfterPass()
    {
        if (Transfer is { } t)
        {
            if (t.Kind == P4Transfer.Outdoors)
            {
                Transfer = null;
                Load(t.Map, t.X, t.Y, jump: true);
                return;
            }
            _stage = Stage.Stopped;
            Prompt = P4OutdoorPrompt.Left;
            return;
        }
        if (Drift >= 1)
        {
            StartMove(P4Dice.Rand(_rng, 8) - 1, forced: true);
            return;
        }
        _stage = Stage.Idle;
    }

    // --- Moving (_omove, _ochkedge) -----------------------------------------------------------------------------------

    private void StartMove(int dir, bool forced)
    {
        _forced = forced;
        int nx = X + Dx[dir], ny = Y - Dy[dir];
        const int last = P4OutdoorMap.Size - 1;
        if (nx is < 0 or > last || ny is < 0 or > last)
        {
            CrossEdge(dir);
            return;
        }
        var type = P4OutdoorMap.TypeOf(MapData!.Tile(nx, ny));
        if (type == ShiningSea && Party.StoryFlags[BarrierFlag] == 0)
        {
            if (Party.Ship == P4ShipState.Aboard)
            {
                _window.Print(GameText.S("p4x.0x43079"));
                _window.Print(GameText.S("p4x.0x43091"));
                _stage = Stage.Barrier;
                WaitKey();
                return;
            }
            _window.Print(GameText.S("p4x.0x4305d"));
            MoveDone(moved: false);
            return;
        }
        if (Blocked(type))
        {
            Party.Sounds.Note(P4Sounds.OutdoorBlocked); // _omove 0x14AC0: _sdnoi when _ochkedge refuses
            MoveDone(moved: false);
            return;
        }
        (OldX, OldY, X, Y) = (X, Y, nx, ny);
        MoveDone(moved: true);
    }

    /// <summary>
    /// <c>_ochkedge</c>: rapids "Those are rapids.", the deep River of Death (type 10) and the open sea (19) always block; the
    /// River of Hell (9) unless the party holds both Charon's Raft and Charon's Staff (pack or members).
    /// </summary>
    private bool Blocked(int type)
    {
        if (type == T.CharonRiverType)
        {
            if (T.CharonItems.All(Party.HasItem))
                return false;
            _window.Print(GameText.S("p4x.0x430f7"));
            return true;
        }
        if (!T.BlockedTypes.Contains(type))
            return false;
        _window.Print(GameText.S(type switch { Rapids => "p4x.0x430e3", 10 => "p4x.0x4310d", _ => "p4x.0x43123" }));
        return true;
    }

    /// <summary>
    /// Stepping off the 26 × 26 map (<c>_omove</c>): the neighbour in the grid (5 wide, 3 in the Netherworld), entering on the
    /// opposite edge; a diagonal step keeps whichever coordinate stays on the map. The arrival tile is not checked.
    /// </summary>
    private void CrossEdge(int dir)
    {
        var (map, x, y) = Step(Map, X, Y, dir);
        if (!_atlas.Outdoors.ContainsKey(map))
            throw new InvalidDataException($"Outdoor map {Map} ({X}, {Y}) leads off the grid to map {map}.");
        if (_forced)
            Drift--;
        AfterStep();
        Load(map, x, y);
    }

    /// <summary>
    /// Where a step in direction <paramref name="dir"/> from (x, y) of <paramref name="map"/> lands (<c>_omove</c>): the
    /// same map, or across the edge into the grid neighbour (5 wide, 3 in the Netherworld from map 16) on the opposite
    /// edge; a diagonal keeps the coordinate that stays on the map and changes map only along the axes that left it.
    /// </summary>
    public static (int Map, int X, int Y) Step(int map, int x, int y, int dir)
    {
        const int last = P4OutdoorMap.Size - 1;
        int dx = Dx[dir], dy = Dy[dir], w = P4Atlas.GridWidth(map);
        int nx = x + dx, ny = y - dy;
        bool xOut = nx is < 0 or > last, yOut = ny is < 0 or > last;
        if (!xOut && !yOut)
            return (map, nx, ny);
        if (dx != 0 && dy != 0)
            return (map + (xOut ? dx : 0) - (yOut ? dy * w : 0),
                xOut ? (nx < 0 ? last : 0) : nx, yOut ? (ny < 0 ? last : 0) : ny);
        return (map + dx - w * dy, dy == 0 ? (dx < 0 ? last : 0) : x, dx == 0 ? (dy > 0 ? last : 0) : y);
    }

    private void MoveDone(bool moved)
    {
        if (_forced)
        {
            Drift--;
        }
        if (moved)
        {
            AfterStep();
            _stage = Stage.PassTop;
            return;
        }
        _stage = Drift >= 1 ? Stage.AfterPass : Stage.Idle;
    }

    /// <summary>D4-039 manual: Levitation's steps run down; D4-038 manual: a ship sailed counts its sea steps.</summary>
    private void AfterStep()
    {
        if (Party.LevitationSteps is int left)
        {
            left--;
            Party.LevitationSteps = left > 0 ? left : null;
            if (left <= 0)
                Party.Levitating = false;
        }
        if (Party.Ship == P4ShipState.Aboard)
            Party.ShipSeaSteps++;
    }

    // --- Saves --------------------------------------------------------------------------------------------------------

    public P4OutdoorSnapshot Capture() => new(
        Map, X, Y, OldX, OldY, Drift, Prompt, [.. _window.Lines], PendingFight, Transfer, _stage.ToString(), _ask.ToString(),
        _lastType, _watf, _check, _einf3, _fightAfterWait, _forced, MapOverview, OverviewBlock);

    public void Restore(P4OutdoorSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        (Map, X, Y, OldX, OldY, Drift) = (s.Map, s.X, s.Y, s.OldX, s.OldY, s.Drift);
        Prompt = s.Prompt;
        _window.Restore(s.Box);
        PendingFight = s.PendingFight;
        Transfer = s.Transfer;
        _stage = Enum.Parse<Stage>(s.Stage);
        _ask = Enum.Parse<Ask>(s.Ask);
        (_lastType, _watf, _check, _einf3, _fightAfterWait, _forced, MapOverview) =
            (s.LastType, s.Watf, s.Check, s.Einf3, s.FightAfterWait, s.Forced, s.MapOverview);
        OverviewBlock = s.OverviewBlock >= 0 ? s.OverviewBlock : T.Overview.BlockFor(Map, -1, jump: true);
    }
}

/// <summary>Everything a <see cref="P4OutdoorLoop"/> needs to carry on where it stopped (the party is saved on its own).</summary>
public sealed record P4OutdoorSnapshot(
    int Map, int X, int Y, int OldX, int OldY, int Drift, P4OutdoorPrompt Prompt, string[] Box, P4OutdoorFight? PendingFight,
    P4Transfer? Transfer, string Stage, string Ask, int LastType, bool Watf, bool Check, bool Einf3,
    P4OutdoorFight? FightAfterWait, bool Forced, bool MapOverview, int OverviewBlock = -1);
