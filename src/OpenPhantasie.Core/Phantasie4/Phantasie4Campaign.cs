namespace OpenPhantasie.Core;

/// <summary><c>_cmapf</c> / <c>_cnewf</c>: the kind of map the party is on (<c>_game</c> runs one loop per kind).</summary>
public enum P4Place
{
    Town = P4Transfer.Town,
    Outdoors = P4Transfer.Outdoors,
    Dungeon = P4Transfer.Dungeon,
}

/// <summary>Where the party is: <c>_cmapf</c>, <c>_cmapn</c>, <c>_cmapx</c> / <c>_cmapy</c> (<c>FORMAT.md</c> §6.7 bytes 0–3).</summary>
public sealed record P4Location(P4Place Place, int Map, int X, int Y);

/// <summary>
/// A Phantasie IV game in play (T6.3): the party record, the rules the save is locked to and the map-kind loop
/// (<c>_game</c>: <c>_town</c> / <c>_outdoor</c> / <c>_dungeon</c> by <c>_cnewf</c>). IV's records and loops are its own,
/// so this is not a <see cref="GameSession"/> campaign: it holds one loop per map kind — <see cref="Town"/>,
/// <see cref="Outdoor"/>, <see cref="Dungeon"/> — whose prompts the caller answers directly, and <see cref="Follow"/>
/// carries the party from the loop it left to the next (<c>_game</c>'s switch).
/// <para>
/// A new game starts in town 1, Pendragon, with an empty party (<c>_load(0)</c> → <c>_init</c>, <c>FORMAT.md</c> §6.1):
/// <c>Phantasie4World.NewGame</c>.
/// </para>
/// </summary>
public sealed class Phantasie4Campaign
{
    private readonly P4Tables _tables;
    private readonly IRandomSource _rng;
    private readonly Func<int, string> _itemName;
    private readonly P4BattleData? _battle;
    private P4Place _place;
    private int _map, _x, _y;
    private bool _started;
    private bool _menuInDungeon;

    public Phantasie4Campaign(GameOptions options, P4Tables tables, P4Atlas atlas, P4Party party, IRandomSource rng,
        Func<int, P4DungeonFile> maps, Func<int, string> itemName, P4Location start, P4BattleData? battle = null,
        P4ServiceData? services = null)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _itemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
        _battle = battle;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.Game != GameId.P4)
            throw new ArgumentException($"IV runs on IV's rules, not {options.Game.Prefix()}'s.", nameof(options));
        ArgumentNullException.ThrowIfNull(start);
        Party = party ?? throw new ArgumentNullException(nameof(party));
        Dungeon = new P4Dungeon(options, tables, party, rng, maps, itemName);
        Outdoor = new P4OutdoorLoop(options, tables, atlas, party, rng);
        Town = new P4TownLoop(options, tables, atlas, party, rng, services);
        Services = services;
        Menus = services is null || tables.Services is null ? null : new P4Menus(options, tables, party, rng, services);
        (_place, _map, _x, _y) = (start.Place, start.Map, start.X, start.Y);
    }

    /// <summary>IV's tables the game runs on.</summary>
    public P4Tables Tables => _tables;

    /// <summary>The rules this game is locked to (T5.0).</summary>
    public GameOptions Options { get; }

    public P4Party Party { get; }

    /// <summary>The location-map loop. Its prompts are answered on it directly; the campaign reads where it left to.</summary>
    public P4Dungeon Dungeon { get; }

    /// <summary>The outdoor loop.</summary>
    public P4OutdoorLoop Outdoor { get; }

    /// <summary>The town loop.</summary>
    public P4TownLoop Town { get; }

    /// <summary>Items, names and the Hall for the buildings and the party menus (null in tests that do without).</summary>
    public P4ServiceData? Services { get; }

    /// <summary>The party menus F / P / C / U of every place (null without <see cref="Services"/>).</summary>
    public P4Menus? Menus { get; }

    /// <summary>
    /// Opens a party menu at the place the party is (town, outdoors, dungeon). Answer its prompts on <see cref="Menus"/>, then
    /// call <see cref="Follow"/>: a menu that moved the party (Transportation, The Box) takes it there.
    /// </summary>
    public void OpenMenu(P4MenuCommand command)
    {
        if (Menus is null)
            throw new InvalidOperationException("The party menus need IV's service data.");
        var place = _place switch
        {
            P4Place.Town => P4CastPlace.Town,
            P4Place.Outdoors => P4CastPlace.Outdoors,
            _ => P4CastPlace.Dungeon,
        };
        Menus.Open(command, place, Location.Map, Party.EncounterLevel);
        _menuInDungeon = _place == P4Place.Dungeon;
        Follow();
    }

    /// <summary>The fight on the 7 × 7 field, while one runs (<see cref="P4Battle.Prompt"/>).</summary>
    public P4Battle? Battle { get; private set; }

    /// <summary>The map kind whose loop runs (<c>_cmapf</c>).</summary>
    public P4Place Place => _place;

    /// <summary>
    /// Where the party is: the running loop's map and square (a town has none: 0, 0). Before <see cref="Start"/> it is the
    /// start location; a loop that has left (its Left prompt, before <see cref="Follow"/>) reports where it is going.
    /// </summary>
    public P4Location Location
    {
        get
        {
            if (!_started)
                return new P4Location(_place, _map, _x, _y);
            switch (_place)
            {
                case P4Place.Dungeon:
                    if (Dungeon.Prompt == P4DungeonPrompt.Left && Dungeon.Transfer is { } t)
                        return new P4Location((P4Place)t.Kind, t.Map, t.X, t.Y);
                    return new P4Location(P4Place.Dungeon, Dungeon.Map.Map, Dungeon.X, Dungeon.Y);
                case P4Place.Outdoors:
                    if (Outdoor.Prompt == P4OutdoorPrompt.Left && Outdoor.Transfer is { } o)
                        return new P4Location((P4Place)o.Kind, o.Map, o.X, o.Y);
                    return new P4Location(P4Place.Outdoors, Outdoor.Map, Outdoor.X, Outdoor.Y);
                default:
                    if (Town.Prompt == P4TownPrompt.Left && Town.Transfer is { } w)
                        return new P4Location((P4Place)w.Kind, w.Map, w.X, w.Y);
                    return new P4Location(P4Place.Town, Town.Town, 0, 0);
            }
        }
    }

    /// <summary>
    /// Whether <see cref="Capture"/> works now: not in a fight, not inside a town building or a party menu (the original
    /// saves from the System icon of a place's main menu). In a location map under <c>p4.dungeon-reload</c> =
    /// <c>x68000</c> also only at the command prompt (<see cref="SaveRefusedMidEvent"/>).
    /// </summary>
    public bool CanSave => Battle is null && (_place != P4Place.Town || !Town.InBuilding)
        && (_place != P4Place.Outdoors || !Outdoor.WaitingForFight && Outdoor.Prompt != P4OutdoorPrompt.Fight)
        && (Menus?.Prompt ?? P4MenuPrompt.Closed) == P4MenuPrompt.Closed
        && !SaveRefusedMidEvent;

    /// <summary>
    /// <c>p4.dungeon-reload</c> = <c>x68000</c> (user s111): the original saves in a location map only from <c>_dmain</c>'s
    /// menu (<c>_syscon</c>), so a save there is refused unless the loop waits at its command prompt. Its reload drops the
    /// loop state anyway (<see cref="ReloadsDungeon"/>).
    /// </summary>
    public bool SaveRefusedMidEvent =>
        ReloadsDungeon(Options) && _started && _place == P4Place.Dungeon && Dungeon.Prompt != P4DungeonPrompt.Action;

    /// <summary>Whether loading a location-map save re-reads the map as the original does (<c>p4.dungeon-reload</c>, user s111).</summary>
    public static bool ReloadsDungeon(GameOptions o) => !o.Is(P4RuleOptions.DungeonReload, "saved-map");

    /// <summary>True once a loop runs (<see cref="Start"/>, <see cref="EnterDungeon"/> or a restored game).</summary>
    public bool LoopBuilt => _started;

    /// <summary>"The party has been wiped out.": the game is over (<c>_deadmsg</c>).</summary>
    public bool PartyLost => _started && _place switch
    {
        P4Place.Dungeon => Dungeon.Prompt == P4DungeonPrompt.PartyLost,
        P4Place.Outdoors => Outdoor.Prompt == P4OutdoorPrompt.PartyLost,
        _ => false,
    };

    /// <summary>Runs the start location's loop (a town entry, or the arrival step outdoors / on a location map).</summary>
    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("The game is already running.");
        GoTo(new P4Transfer((int)_place, _map, _x, _y, Facing));
    }

    /// <summary>
    /// <c>_odungeon</c> answered yes, then <c>_dungeon</c>: map <paramref name="map"/> with the party on (x, y). The facing is
    /// kept: neither <c>_odungeon</c> nor <c>_dload</c> writes <c>_dire</c> (only <c>_dmove</c> and SETDIR do) `[C]`.
    /// </summary>
    public void EnterDungeon(int map, int x, int y)
    {
        var facing = Facing;
        _started = true;
        _place = P4Place.Dungeon;
        Dungeon.Enter(map, x, y, facing);
    }

    /// <summary>
    /// <c>_game</c>'s switch: while the running loop has left (its Left prompt), the party is moved into the loop of the
    /// target's map kind. Call after answering a prompt; does nothing otherwise.
    /// </summary>
    public void Follow()
    {
        if (Menus is { Prompt: P4MenuPrompt.Closed, Transfer: { } moved })
        {
            Menus.Transfer = null;
            GoTo(moved);
        }
        else if (_menuInDungeon && Menus is { Prompt: P4MenuPrompt.Closed } && _place == P4Place.Dungeon
                 && Dungeon.Prompt == P4DungeonPrompt.Action)
        {
            // _dmain: after a front-end screen the loop makes its pass (P4DungeonCommand.Other).
            _menuInDungeon = false;
            Dungeon.Act(P4DungeonCommand.Other);
        }
        while (_started)
        {
            if (FollowBattle())
                return;
            P4Transfer? t = _place switch
            {
                P4Place.Dungeon => Dungeon.Prompt == P4DungeonPrompt.Left ? Dungeon.Transfer : null,
                P4Place.Outdoors => Outdoor.Prompt == P4OutdoorPrompt.Left ? Outdoor.Transfer : null,
                _ => Town.Prompt == P4TownPrompt.Left ? Town.Transfer : null,
            };
            if (t is null)
                return;
            GoTo(t);
        }
    }

    /// <summary>
    /// Starts the battle a loop's Fight prompt waits for, or hands a finished one's outcome to its loop. True while a battle
    /// waits for the player.
    /// </summary>
    private bool FollowBattle()
    {
        if (_battle is null)
            return false;
        while (true)
        {
            if (Battle is { Done: true } done)
            {
                Battle = null;
                Party.EncounterLevel = done.EncounterLevel; // _emlevel stays after the fight (Monster Evaluation reads it)
                var move = done.Outcome == P4FightOutcome.Lost ? null : ReturnSquare(done.End);
                if (_place == P4Place.Dungeon)
                    Dungeon.FightOver(done.Outcome == P4FightOutcome.Lost, move);
                else
                    Outdoor.FightOver(done.Outcome, move);
            }
            if (Battle is not null)
                return true;
            if (_place == P4Place.Dungeon && Dungeon.Prompt == P4DungeonPrompt.Fight)
                Battle = P4Battle.Dungeon(Options, _tables, _battle, Party, _rng, Dungeon.PendingFight!.Encounter,
                    Dungeon.PendingFight.Terrain);
            else if (_place == P4Place.Outdoors && Outdoor.Prompt == P4OutdoorPrompt.Fight)
                Battle = P4Battle.Outdoor(Options, _tables, _battle, Party, _rng, Outdoor.PendingFight!, Outdoor.TerrainType);
            else
                return false;
            Battle.ItemName = _itemName;
        }
    }

    /// <summary>
    /// The square the party goes to after a fight that ended with <paramref name="end"/> (<see cref="P4FightReturn"/>), or
    /// null to stay. A random square that finds no candidate stays <c>[~]</c>.
    /// </summary>
    private P4FightMove? ReturnSquare(P4BattleEnd end)
    {
        var outdoors = _place == P4Place.Outdoors;
        switch (P4FightReturn.For(Options, end, outdoors))
        {
            case P4FightReturnKind.Previous:
                if (outdoors)
                    return new P4FightMove(Outdoor.OldX, Outdoor.OldY);
                // A fight on the arrival square: no previous square on this map yet [~].
                return Dungeon.NoStepSinceLoad ? null : new P4FightMove(Dungeon.OldX, Dungeon.OldY);
            case P4FightReturnKind.Entrance:
                return new P4FightMove(Dungeon.EntryX, Dungeon.EntryY);
            case P4FightReturnKind.Random:
            {
                var sq = outdoors
                    ? P4FightReturn.RandomOutdoorSquare(Outdoor.MapData!, _battle!.Tables, _rng)
                    : P4FightReturn.RandomDungeonSquare(Dungeon.Map, _rng);
                // A ship stays moored where the party fought: it cannot follow onto land.
                return sq is var (x, y) ? new P4FightMove(x, y, MoorHere: false) : null;
            }
            default:
                return null;
        }
    }

    private void GoTo(P4Transfer t)
    {
        _started = true;
        switch ((P4Place)t.Kind)
        {
            case P4Place.Dungeon:
                EnterDungeon(t.Map, t.X, t.Y);
                break;
            case P4Place.Outdoors:
                _place = P4Place.Outdoors;
                Outdoor.Arrive(t.Map, t.X, t.Y);
                break;
            default:
                _place = P4Place.Town;
                Town.Arrive(t.Map);
                break;
        }
    }

    /// <summary><c>_dire</c>: 0 N, 1 E, 2 S, 3 W (BSS, so 0 before any move).</summary>
    public int Facing => Dungeon.Dir;

    // --- Saves --------------------------------------------------------------------------------------------------------

    /// <summary>The whole game: rules, party, place and every loop that has run.</summary>
    /// <exception cref="InvalidOperationException">A fight is running: IV cannot be saved in combat.</exception>
    public P4CampaignSnapshot Capture()
    {
        if (Battle is not null)
            throw new InvalidOperationException("A fight cannot be saved.");
        if (!CanSave)
            throw new InvalidOperationException("A building or a menu is open; close it before saving.");
        var loc = Location;
        return new P4CampaignSnapshot(
            GameOptionsSnapshot.From(Options), P4PartySnapshot.From(Party), _place, loc.Map, loc.X, loc.Y,
            Facing, Dungeon.HasMap ? Dungeon.Capture() : null, _started,
            Outdoor.Map != 0 ? Outdoor.Capture() : null, Town.Town != 0 ? Town.Capture() : null);
    }

    /// <summary>
    /// A game from <paramref name="s"/>: the party and rules come from the save; tables, maps and text from the data
    /// (<paramref name="rng"/> is a fresh stream, as for every remaster save).
    /// </summary>
    public static Phantasie4Campaign Restore(P4CampaignSnapshot s, P4Tables tables, P4Atlas atlas, IRandomSource rng,
        Func<int, P4DungeonFile> maps, Func<int, string> itemName, P4BattleData? battle = null,
        P4ServiceData? services = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        var party = s.Party.ToParty();
        var c = new Phantasie4Campaign(s.Options.ToOptions(GameId.P4), tables, atlas, party, rng, maps, itemName,
            new P4Location(s.Place, s.Map, s.X, s.Y), battle, services);
        var inDungeon = s.Started && s.Place == P4Place.Dungeon;
        var reload = ReloadsDungeon(c.Options);
        if (inDungeon && s.Dungeon is null)
            throw new InvalidDataException("A save in a dungeon must hold the dungeon loop.");
        if (s.Dungeon is { } d && !reload)
            c.Dungeon.Restore(d);
        else
            c.Dungeon.Face(s.Facing); // the original keeps no map (a later entry loads it fresh); facing kept [~]
        if (s.Outdoor is { } o)
            c.Outdoor.Restore(o);
        if (s.Town is { } t)
            c.Town.Restore(t);
        if (s.Started && ((s.Place == P4Place.Outdoors && s.Outdoor is null) || (s.Place == P4Place.Town && s.Town is null)))
            throw new InvalidDataException("A save must hold the loop it is in.");
        c._started = s.Started;
        // The Guild treasury collects on loading a save in town (11-guild-treasury.md 3); nothing when it is off.
        if (s.Started && s.Place == P4Place.Town && c.Town.Town != 0)
            c.Party.SweepTreasury(c.Options, inTown: true);
        if (inDungeon && reload)
        {
            // The original's load (FORMAT.md §6): _game → _dungeon → _dload at the saved square, the map read fresh from
            // O_0xx, one more _dreset step, _prex on the fresh pictures, the map-load event and its encounter roll. The
            // saved loop state is not used; Examine's visit counts, the encounter pool and the rate start as on a fresh
            // run [~] (the original keeps whatever its memory held when loading without a reboot).
            c.Dungeon.Enter(s.Map, s.X, s.Y, s.Facing);
            c.Follow(); // a fight or a move the load event starts
        }
        return c;
    }
}

/// <summary>
/// A saved IV game (<see cref="Phantasie4Campaign.Capture"/>): Map / X / Y are <see cref="Phantasie4Campaign.Location"/>;
/// each loop that ran is saved whole.
/// </summary>
public sealed record P4CampaignSnapshot(
    GameOptionsSnapshot Options, P4PartySnapshot Party, P4Place Place, int Map, int X, int Y, int Facing,
    P4DungeonSnapshot? Dungeon, bool Started = false, P4OutdoorSnapshot? Outdoor = null, P4TownSnapshot? Town = null);

/// <summary>A saved IV member's record fields beyond the basic sheet (<see cref="P4MemberSnapshot.More"/>).</summary>
public sealed record P4MemberMore(
    int[] ItemCharges, int[] Abilities, int[] Wounds, long Experience, long Account, int AgingFactor, int AgeUnits,
    bool HasAged, int Town, int[]? Spells = null, int[]? Strategy = null, P4MemberRecord? Record = null, int Lifespan = 0);

/// <summary>A saved IV member's record bytes the guild, training and leaving a town read (+1A, +09, +1C, +36, +72, +3B, +01, +B9, +29).</summary>
public sealed record P4MemberRecord(
    int Social, int Score, int Origin, int MagicLevel, bool MpGrowth, int MeleeBonus, int GuildSlot,
    int SavedRace = 0, int MoveBase = 0, P4AncestorRef[]? Ancestors = null);

/// <summary>A saved IV member: every field <see cref="P4Member"/> holds.</summary>
public sealed record P4MemberSnapshot(
    string Name, int Race, int Class, int Level, int Condition, int HpMax, int Hp, int MpMax, int Mp, int Defence,
    int[] Skills, int[] Items, P4MemberMore More)
{
    public static P4MemberSnapshot From(P4Member m) => new(
        m.Name, m.Race, m.Class, m.Level, m.Condition, m.HpMax, m.Hp, m.MpMax, m.Mp, m.Defence, [.. m.Skills], [.. m.Items],
        new P4MemberMore([.. m.ItemCharges], [.. m.Abilities], [.. m.Wounds], m.Experience, m.Account, m.AgingFactor,
            m.AgeUnits, m.HasAged, m.Town, [.. m.Spells], [.. m.Strategy],
            new P4MemberRecord(m.Social, m.Score, m.Origin, m.MagicLevel, m.MpGrowth, m.MeleeBonus, m.GuildSlot,
                m.SavedRace, m.MoveBase, [.. m.Ancestors]), m.Lifespan));

    public P4Member ToMember()
    {
        if (Skills.Length != P4Ids.Skills || Items.Length != P4Member.CarriedSlots || More is null
            || More.ItemCharges.Length != P4Member.CarriedSlots || More.Abilities.Length != 6
            || More.Wounds.Length != P4Body.Parts)
            throw new InvalidDataException($"{Name}: the saved member record has the wrong size.");
        return new P4Member
        {
            Name = Name, Race = Race, Class = Class, Level = Level, Condition = Condition, HpMax = HpMax, Hp = Hp,
            MpMax = MpMax, Mp = Mp, Defence = Defence, Skills = [.. Skills], Items = [.. Items],
            ItemCharges = [.. More.ItemCharges], Abilities = [.. More.Abilities], Wounds = [.. More.Wounds],
            Experience = More.Experience, Account = More.Account, AgingFactor = More.AgingFactor, AgeUnits = More.AgeUnits,
            Lifespan = More.Lifespan,
            HasAged = More.HasAged, Town = More.Town,
            Spells = More.Spells is { Length: P4Member.SpellCount } sp ? [.. sp] : new int[P4Member.SpellCount],
            Strategy = More.Strategy is { Length: 5 } st ? [.. st] : [.. P4Member.DefaultStrategy],
            Social = More.Record?.Social ?? 0, Score = More.Record?.Score ?? 0, Origin = More.Record?.Origin ?? 0,
            MagicLevel = More.Record?.MagicLevel ?? 0, MpGrowth = More.Record?.MpGrowth ?? false,
            MeleeBonus = More.Record?.MeleeBonus ?? 0,
            GuildSlot = More.Record?.GuildSlot ?? -1, SavedRace = More.Record?.SavedRace ?? 0,
            MoveBase = More.Record?.MoveBase ?? 0,
            Ancestors = More.Record?.Ancestors is { Length: 2 } an ? [.. an] : [P4AncestorRef.None, P4AncestorRef.None],
        };
    }
}

/// <summary>A saved IV party's travel fields: days, the experience pool and the ship (<see cref="P4PartySnapshot.Travel"/>).</summary>
public sealed record P4PartyTravel(
    int DaysOut, int Daytime, long ExperiencePool, P4ShipState Ship, int ShipNavigator, int ShipMap, int ShipX, int ShipY,
    int ShipSeaSteps, long GoldSinceTown = 0);

/// <summary>A saved IV guild and shop: the roster (<c>guils</c>), <c>_par</c>+0A, <c>goods</c> and the live shop stock.</summary>
public sealed record P4PartyGuild(
    P4MemberSnapshot?[] Roster, int MaxScore, int[] GoodsCount, int[] GoodsTown, int[] ShopStock,
    int EncounterLevel = 0, int?[]? HallDescendants = null, P4HallPerson?[]? HallImports = null);

/// <summary>
/// A saved IV party record: every field <see cref="P4Party"/> holds. <paramref name="Treasury"/>: the Guild treasury
/// (<c>11-guild-treasury.md</c>), null while it is empty (a save without it loads an empty one).
/// </summary>
public sealed record P4PartySnapshot(
    P4MemberSnapshot[] Members, long Gold, int AverageLevel, bool Levitating, int? LevitationSteps, int[] Pack,
    int[] PackCharges, byte[] StoryFlags, byte[] LocationFlags, P4PartyTravel Travel, int? Elemental = null,
    P4PartyGuild? Guild = null, TreasurySnapshot? Treasury = null)
{
    public static P4PartySnapshot From(P4Party p) => new(
        [.. p.Members.Select(P4MemberSnapshot.From)], p.Gold, p.AverageLevel, p.Levitating, p.LevitationSteps, [.. p.Pack],
        [.. p.PackCharges], [.. p.StoryFlags], [.. p.LocationFlags],
        new P4PartyTravel(p.DaysOut, p.Daytime, p.ExperiencePool, p.Ship, p.ShipNavigator, p.ShipMap, p.ShipX, p.ShipY,
            p.ShipSeaSteps, p.GoldSinceTown), p.Elemental,
        new P4PartyGuild([.. p.Roster.Select(m => m is null ? null : P4MemberSnapshot.From(m))], p.MaxScore,
            [.. p.GoodsCount], [.. p.GoodsTown], [.. p.ShopStock], p.EncounterLevel, [.. p.HallDescendants],
            [.. p.HallImports]),
        p.Treasury.Balance == 0 && p.Treasury.Trip == 0 && p.Treasury.Ledger.Count == 0 ? null : p.Treasury.ToSnapshot());

    public P4Party ToParty()
    {
        if (Pack.Length != P4Party.PackSlots || PackCharges.Length != P4Party.PackSlots
            || StoryFlags.Length != P4Party.StoryFlagCount
            || LocationFlags.Length != P4Party.LocationMaps * P4Party.LocationFlagsPerMap || Travel is null)
            throw new InvalidDataException("The saved party record has the wrong size.");
        var p = new P4Party
        {
            Gold = Gold, AverageLevel = AverageLevel, Levitating = Levitating, LevitationSteps = LevitationSteps,
            DaysOut = Travel.DaysOut, Daytime = Travel.Daytime, ExperiencePool = Travel.ExperiencePool, Ship = Travel.Ship,
            ShipNavigator = Travel.ShipNavigator, ShipMap = Travel.ShipMap, ShipX = Travel.ShipX, ShipY = Travel.ShipY,
            ShipSeaSteps = Travel.ShipSeaSteps, GoldSinceTown = Travel.GoldSinceTown, Elemental = Elemental,
            Treasury = OpenPhantasie.Core.Treasury.From(Treasury),
        };
        p.Members.AddRange(Members.Select(m => m.ToMember()));
        Pack.CopyTo(p.Pack, 0);
        PackCharges.CopyTo(p.PackCharges, 0);
        StoryFlags.CopyTo(p.StoryFlags, 0);
        LocationFlags.CopyTo(p.LocationFlags, 0);
        if (Guild is { } g)
        {
            if (g.Roster.Length != P4Party.RosterSlots || g.GoodsCount.Length != P4Party.ItemIds
                || g.GoodsTown.Length != P4Party.ItemIds || g.ShopStock.Length != P4Party.ItemIds)
                throw new InvalidDataException("The saved guild record has the wrong size.");
            for (var i = 0; i < P4Party.RosterSlots; i++)
                p.Roster[i] = g.Roster[i]?.ToMember();
            (p.MaxScore, p.EncounterLevel) = (g.MaxScore, g.EncounterLevel);
            g.GoodsCount.CopyTo(p.GoodsCount, 0);
            g.GoodsTown.CopyTo(p.GoodsTown, 0);
            g.ShopStock.CopyTo(p.ShopStock, 0);
            if (g.HallDescendants is { Length: P4Party.HallSlots } hd)
                hd.CopyTo(p.HallDescendants, 0);
            if (g.HallImports is { Length: P4Party.HallSlots } hi)
                hi.CopyTo(p.HallImports, 0);
        }
        return p;
    }
}
