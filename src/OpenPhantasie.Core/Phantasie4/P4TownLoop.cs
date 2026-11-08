namespace OpenPhantasie.Core;

/// <summary>What the town loop is waiting for (<see cref="P4TownLoop.Prompt"/>).</summary>
public enum P4TownPrompt
{
    /// <summary>The town's buildings (<see cref="P4TownLoop.Buildings"/>; <see cref="P4TownLoop.Enter"/>).</summary>
    Menu,

    /// <summary>A share 0–3 per member (<see cref="P4TownLoop.SharesFor"/>; <see cref="P4TownLoop.EnterShares"/>).</summary>
    Shares,

    /// <summary>"SPACE" (<see cref="P4TownLoop.Continue"/>).</summary>
    Wait,

    /// <summary>A yes / no question (<see cref="P4TownLoop.Answer"/>).</summary>
    Confirm,

    /// <summary>The party left for the outdoors (<see cref="P4TownLoop.Transfer"/>).</summary>
    Left,

    /// <summary>
    /// The game is over: Pendragon with the first ending flag ran the ending (<c>_ending</c>), whose final screen is in
    /// <see cref="P4TownLoop.Box"/> (score, completion code). s158: it asks to save there
    /// (<see cref="P4TownLoop.EndingStep"/>, <see cref="P4TownLoop.AnswerEnding"/>), then offers End game / Back to title.
    /// </summary>
    Ending,

    /// <summary>A building's list (<see cref="P4TownLoop.Choices"/>; <see cref="P4TownLoop.Choose"/>, −1 = SPACE).</summary>
    Pick,

    /// <summary>A building asks for a member (<see cref="P4TownLoop.WhoEnabled"/>; <see cref="P4TownLoop.Choose"/>).</summary>
    Who,

    /// <summary>An amount (<see cref="P4TownLoop.NumberLimit"/>; <see cref="P4TownLoop.EnterNumber"/>).</summary>
    Number,

    /// <summary>A name (<see cref="P4TownLoop.TextLimit"/>; <see cref="P4TownLoop.EnterText"/>).</summary>
    Text,
}

/// <summary>s158: the final screen's steps (<c>_endstory</c>): none yet, the save question open, answered.</summary>
public enum P4EndingStep { None, Question, Answered }

/// <summary>A town's doors (<c>_town</c>, <c>_bvs</c>): 0–4 in every town, 5 by town type, then Leave.</summary>
public enum P4Building { Guild, Mystic, Armory, Bank, Inn, HallOfGlory, Shipwright, Leave }

/// <summary>
/// What the original shows in the town's picture area (s107, T6.4; P4.X's <c>_xunpack</c> calls, <c>FORMAT.md</c> §6.9):
/// the street (<c>_tload</c>), a building's picture, the Hall, a Hall history page, or nothing — the roster, creation,
/// training, magic, the Armory, Trade, Distribute and the ending clear it (<c>_clrtdraw</c>).
/// </summary>
public enum P4TownScene
{
    Street, None, Guild, Inn, Bank, Shipwright, Mystic, Hall, HallHistory,

    /// <summary>The ending (<c>ending.md</c> §1): Pendragon's street with END1 (<c>_endset</c>), the banquet END2
    /// (<c>_banquet</c>), Arathamus in his cell END3 (<c>_arathamus</c>). s162: the final screen (<c>_endstory</c>) opens
    /// its own titled window 32 over them (<c>_wopen(32, 160, 32, 312, 352, 1, "Phantasie IV")</c>): no picture.</summary>
    EndingStreet, EndingBanquet, EndingCell, EndingFinal,
}

/// <summary>s162: one Town Gate row (<c>_townent</c>): the member's place in the party, name and the line's string id.</summary>
public sealed record P4GateRow(int Member, string Name, string Line);

/// <summary>What a <see cref="P4TownPrompt.Shares"/> prompt shares out.</summary>
public enum P4ShareKind { Gold, Experience }

/// <summary>
/// Phantasie IV's town loop (<c>_town</c>; spec <c>reverse-engineering/x68000/phantasie4/logic-specs/towns.md</c>): the
/// entry (<c>_townent</c>: D4-030's gold deposit, the experience share-out, aging; <c>_welcome</c>'s item distribution; the
/// ending in Pendragon; <c>_tload</c>), the buildings and leaving (<c>_exittown</c>) to the town's exit square. Headless and
/// resumable like <see cref="P4OutdoorLoop"/>. The buildings and the T / D commands are in <c>P4TownLoop.Screens.cs</c>;
/// they need <see cref="P4ServiceData"/> (a loop made without it runs the Inn, Mystic, Shipwright, Bank and Leave only).
/// </summary>
public sealed partial class P4TownLoop
{
    /// <summary>The town price factor's town: Pendragon, the only type-0 town; story flag <c>_par</c>+158 starts the ending [C].</summary>
    public const int Pendragon = 1, EndingFlag = 0x38;

    /// <summary>Zeus Juice, which the Mystic counts (towns.md §7) [C].</summary>
    public const int ZeusJuice = 126;

    private readonly GameOptions _options;
    private readonly P4Tables _tables;
    private readonly P4Atlas _atlas;
    private readonly IRandomSource _rng;
    private readonly P4ServiceData? _data;
    private readonly P4MessageWindow _window = new();

    public P4TownLoop(GameOptions options, P4Tables tables, P4Atlas atlas, P4Party party, IRandomSource rng,
        P4ServiceData? data = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        Party = party ?? throw new ArgumentNullException(nameof(party));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _data = data;
    }

    public P4Party Party { get; }

    /// <summary>The town's map number (<c>_cmapn</c>), 0 before the first arrival.</summary>
    public int Town { get; private set; }

    public P4TownSite? Site => Town == 0 ? null : _atlas.Towns[Town];

    public P4TownPrompt Prompt { get; private set; } = P4TownPrompt.Menu;

    public IReadOnlyList<string> Box => _window.Lines;

    /// <summary>What the open <see cref="P4TownPrompt.Shares"/> shares out, and how much (gold, or experience × 10).</summary>
    public P4ShareKind SharesFor { get; private set; }

    public long ShareAmount => SharesFor == P4ShareKind.Gold ? Party.Gold : Party.ExperiencePool * 10;

    /// <summary>The leaving move (<see cref="P4TownPrompt.Left"/>): outdoor map and square.</summary>
    public P4Transfer? Transfer { get; private set; }

    /// <summary>
    /// The town's doors in order (by town type: 0 adds the Hall, 1 the Shipwright, 2 nothing). With no party only the
    /// Guild's door (G) is open (<c>_townmenu</c>: one button and the System icon; <c>FORMAT.md</c> §6.1).
    /// </summary>
    public IReadOnlyList<P4Building> Buildings
    {
        get
        {
            if (Party.Members.Count == 0)
                return [P4Building.Guild];
            var list = new List<P4Building>
                { P4Building.Guild, P4Building.Mystic, P4Building.Armory, P4Building.Bank, P4Building.Inn };
            switch (Site?.Type)
            {
                case 0: list.Add(P4Building.HallOfGlory); break;
                case 1: list.Add(P4Building.Shipwright); break;
            }
            list.Add(P4Building.Leave);
            return list;
        }
    }

    /// <summary>Every building runs (s102); kept for the screen's "not built" mark.</summary>
    public static bool IsBuilt(P4Building b) => Enum.IsDefined(b);

    private enum Stage { Menu, Deposit, Experience, Aging, Distribute, Load, Shipwright, Stopped }

    private Stage _stage = Stage.Menu;

    private P4TownScene _scene = P4TownScene.Street;

    /// <summary>The picture the original shows now (<see cref="P4TownScene"/>); the street whenever the town menu waits.</summary>
    public P4TownScene Scene => Prompt == P4TownPrompt.Menu && _screen is null ? P4TownScene.Street : _scene;

    /// <summary>The Hall history page shown (0–8, <c>_hhist</c>'s SC_A…I) while <see cref="Scene"/> is <see cref="P4TownScene.HallHistory"/>.</summary>
    public int HallHistoryPage { get; private set; }

    // --- Entry --------------------------------------------------------------------------------------------------------

    /// <summary>The party comes to town <paramref name="town"/> (<c>_town</c> → <c>_townent</c> → <c>_welcome</c> → <c>_tload</c>).</summary>
    public void Arrive(int town)
    {
        if (!_atlas.Towns.ContainsKey(town))
            throw new ArgumentOutOfRangeException(nameof(town), $"No town {town}.");
        Town = town;
        Transfer = null;
        GateRows = null;
        InnRows = null;
        _scene = P4TownScene.None; // _town clears the area before _townent / _welcome; _tload draws the street last
        _window.Clear();
        Prompt = P4TownPrompt.Menu;
        _stage = Stage.Deposit;
        Run();
    }

    private void Run()
    {
        while (Prompt == P4TownPrompt.Menu && _screen is null && _stage != Stage.Menu && _stage != Stage.Stopped)
        {
            switch (_stage)
            {
                case Stage.Deposit:
                    _stage = Stage.Experience;
                    // D4-030 manual: the party gold is paid into the accounts by shares first; the code leaves it.
                    if (P4Town.DepositsOnEntry(_options) && Party.Gold > 0 && Party.Members.Count > 0)
                        AskShares(P4ShareKind.Gold);
                    break;
                case Stage.Experience:
                    _stage = Stage.Aging;
                    if (Party.ExperiencePool != 0 && Party.Members.Count > 0)
                        AskShares(P4ShareKind.Experience);
                    break;
                case Stage.Aging:
                    _stage = Stage.Distribute;
                    Age();
                    break;
                case Stage.Distribute:
                    _stage = Stage.Load;
                    // _welcome: the spoils are shared out before the town opens (manual p. 32 note 3).
                    if (_data is not null && Party.Members.Count > 0 && Party.Pack.Any(i => i != 0))
                    {
                        _scene = P4TownScene.None;
                        Open(Distribute(arrival: true));
                    }
                    break;
                case Stage.Load:
                    _stage = Stage.Menu;
                    Load();
                    break;
            }
        }
    }

    private void AskShares(P4ShareKind kind)
    {
        SharesFor = kind;
        Prompt = P4TownPrompt.Shares;
    }

    /// <summary>
    /// The shares for the open <see cref="P4TownPrompt.Shares"/>: 0–3 per member, 0 only for a member who is not Okay (the
    /// "0" button's enable field, towns.md §2.1 <c>[~]</c>).
    /// </summary>
    public void EnterShares(IReadOnlyList<int> shares)
    {
        Require(P4TownPrompt.Shares);
        ArgumentNullException.ThrowIfNull(shares);
        if (shares.Count != Party.Members.Count)
            throw new ArgumentException("One share per member.", nameof(shares));
        for (var i = 0; i < shares.Count; i++)
        {
            var min = Party.Members[i].Condition == P4Member.Okay ? 1 : 0;
            if (shares[i] < min || shares[i] > 3)
                throw new ArgumentOutOfRangeException(nameof(shares), $"{Party.Members[i].Name}: share {shares[i]}.");
        }
        if (_screen is not null)
        {
            _shares = [.. shares];
            Resume();
            return;
        }
        if (SharesFor == P4ShareKind.Gold)
        {
            var accounts = P4Town.Deposit(_options, _tables.Towns, Party.Gold, Party.Members.Select(m => m.Account).ToList(),
                shares);
            for (var i = 0; i < accounts.Length; i++)
                Party.Members[i].Account = accounts[i];
            Party.Gold = 0;
        }
        else
        {
            var xp = P4Town.ShareExperience(Party.ExperiencePool, Party.Members.Select(m => m.Experience).ToList(), shares);
            for (var i = 0; i < xp.Length; i++)
                Party.Members[i].Experience = xp[i];
            Party.ExperiencePool = 0;
        }
        Prompt = P4TownPrompt.Menu;
        Run();
    }

    /// <summary>
    /// Aging (towns.md §2.2, D4-033 / D4-034): every member's age += trunc(days out / 0x700) (cap 65,000), the day counter
    /// is cleared, and each living member may grow older (once: the six abilities and the defence byte decline) or die of
    /// old age (they become 0). Messages wait for a key.
    /// </summary>
    private void Age()
    {
        var add = P4Aging.AgeUnitsFromDays(_tables.Aging, Party.DaysOut);
        var said = false;
        var rows = new List<P4GateRow>();
        foreach (var (m, index) in Party.Members.Select((m, i) => (m, i)))
        {
            m.AgeUnits = Math.Min(P4Aging.AgeCap, m.AgeUnits + add);
            if (!m.Alive)
                continue;
            switch (P4Aging.Check(_options, _tables.Aging, m.Race, m.Abilities[P4Member.Luck], m.AgingFactor, m.AgeUnits,
                        m.HasAged))
            {
                case P4AgeEvent.GrownOlder:
                    m.HasAged = true;
                    for (var i = 0; i < m.Abilities.Length; i++)
                        m.Abilities[i] = P4Aging.ScaleOnAging(_options, m.Abilities[i]);
                    m.Defence = P4Aging.ScaleOnAging(_options, m.Defence);
                    _window.Line(m.Name + GameText.S("p4x.0x3fa6f"));
                    rows.Add(new(index, m.Name, "p4x.0x3fa6f"));
                    said = true;
                    break;
                case P4AgeEvent.DiedOfOldAge:
                    m.Condition = P4Member.Dead;
                    Array.Clear(m.Abilities);
                    m.Defence = 0;
                    _window.Line(m.Name + GameText.S("p4x.0x3fa89"));
                    rows.Add(new(index, m.Name, "p4x.0x3fa89"));
                    said = true;
                    break;
            }
        }
        Party.DaysOut = 0;
        if (said)
        {
            Prompt = P4TownPrompt.Wait;
            GateRows = rows;
        }
    }

    /// <summary>
    /// s162: the Town Gate window's rows (<c>_townent</c>'s <c>_wopen(32, 160, 32, 312, 352, 1, "Town Gate")</c>, opened at
    /// the first member who ages): each aged member's line, at the member's own row, while the aging wait is up; else null.
    /// </summary>
    public IReadOnlyList<P4GateRow>? GateRows { get; private set; }

    /// <summary>
    /// s163: the inn's rows (<c>_inn</c>, FORMAT.md 6.9 "Building windows under the picture") while its SPACE wait is up:
    /// each living member who slept, at the member's own row; else null.
    /// </summary>
    public IReadOnlyList<P4InnRow>? InnRows { get; private set; }

    /// <summary>
    /// <c>_town</c>'s Pendragon test, then <c>_tload</c>: with the first ending flag set Pendragon runs the ending instead of
    /// the town; else every member's current town (+1B, also the guild record's) is this one and, with a party, the shop
    /// restocks (<c>_igen8</c>: this town's sold-back goods, then 8 random items added to the live stock).
    /// </summary>
    /// <summary>
    /// How many times this loop ran <c>_tload</c> (each arrival, and the ending's street): the Client shows the
    /// original's "Welcome!" while the street picture loads (s151).
    /// </summary>
    public int Loads { get; private set; }

    private void Load()
    {
        Loads++;
        if (Town == Pendragon && Party.StoryFlags[EndingFlag] != 0 && _tables.Services is not null && _data is not null
            && P4TownRules.Ending(_tables.Service, Party) is int ending)
        {
            _scene = P4TownScene.EndingStreet; // _endset: _tload(1) draws the street, END1 over its lower part
            Open(Ending(ending));
            return;
        }
        if (Town == Pendragon && Party.StoryFlags[EndingFlag] != 0 && _data is null)
        {
            _stage = Stage.Stopped;
            Prompt = P4TownPrompt.Ending;
            return;
        }
        foreach (var m in Party.Members)
        {
            m.Town = Town;
            if (m.GuildSlot >= 0 && Party.Roster[m.GuildSlot] is { } g)
                g.Town = Town;
        }
        if (Party.Members.Count == 0)
            return;
        for (var i = 0; i < P4Party.ItemIds; i++)
        {
            if (Party.GoodsTown[i] == Town)
                Party.ShopStock[i] = Party.GoodsCount[i];
        }
        foreach (var item in P4Town.RandomShopItems(_tables.Towns, _rng, Party.Members.Sum(m => m.Level)))
            Party.ShopStock[item] = Math.Min(255, Party.ShopStock[item] + 1);
    }

    // --- Buildings ----------------------------------------------------------------------------------------------------

    /// <summary>Goes through a door.</summary>
    public void Enter(P4Building building)
    {
        Require(P4TownPrompt.Menu);
        if (!Buildings.Contains(building))
            throw new ArgumentException($"{Site!.Name} has no {building}.", nameof(building));
        _window.Clear();
        _scene = building switch
        {
            P4Building.Guild => P4TownScene.Guild,
            P4Building.Inn => P4TownScene.Inn,
            P4Building.Bank => P4TownScene.Bank,
            P4Building.Shipwright => P4TownScene.Shipwright,
            P4Building.Mystic => P4TownScene.Mystic,
            P4Building.HallOfGlory => P4TownScene.Hall,
            _ => P4TownScene.None,
        };
        switch (building)
        {
            case P4Building.Inn:
                // D4-032: two weeks under the manual (they age the party at the next town entry), none in the code.
                InnRows = P4Town.InnStay(Party, _window, _tables.Services, _rng, _tables.Towns);
                Party.DaysOut = Math.Min(0xFFFF, Party.DaysOut + P4Town.InnStayDays(_options, _tables.Towns));
                Prompt = P4TownPrompt.Wait;
                break;
            case P4Building.Mystic:
                _window.Line(GameText.S("p4x.0x40237") + MysticScore() + GameText.S("p4x.0x40247"));
                _window.Line(GameText.S(MysticScore() switch
                {
                    <= 30 => "p4x.0x4024f",
                    <= 60 => "p4x.0x40260",
                    <= 90 => "p4x.0x40271",
                    _ => "p4x.0x40284",
                }));
                Prompt = P4TownPrompt.Wait;
                break;
            case P4Building.Shipwright:
                Shipwright();
                break;
            case P4Building.Leave:
                Leave();
                break;
            case P4Building.Bank:
                Open(Bank());
                break;
            case P4Building.Guild when _data is not null:
                Open(Guild());
                break;
            case P4Building.Armory when _data is not null && Party.Members.Count > 0:
                Open(Armory());
                break;
            case P4Building.HallOfGlory when _data is not null:
                Open(Hall());
                break;
            default:
                _window.Line(GameText.S("core.p4.building_not_built"));
                Prompt = P4TownPrompt.Wait;
                break;
        }
    }

    /// <summary>
    /// The Mystic's score (towns.md §7): 3 × story flags 4–10 + 5 × flag 0x2E + 5 × flag 0x27 + 15 × Zeus Juices (pack and
    /// carried) + 2 × the average level, trunc(byte sum of levels / party size), which the visit stores in <c>_par</c>+0B.
    /// </summary>
    private int MysticScore()
    {
        var flags = 0;
        for (var i = 4; i <= 10; i++)
            flags += Party.StoryFlags[i];
        var juice = Party.Pack.Count(i => i == ZeusJuice) + Party.Members.Sum(m => m.Items.Count(i => i == ZeusJuice));
        var levels = (byte)Party.Members.Sum(m => m.Level);
        Party.AverageLevel = Party.Members.Count == 0 ? 0 : levels / Party.Members.Count;
        return 3 * flags + 5 * Party.StoryFlags[0x2E] + 5 * Party.StoryFlags[0x27] + 15 * juice + 2 * Party.AverageLevel;
    }

    /// <summary>The shipwright's price (<c>_shipyard</c>, <c>rule-tables.json</c> <c>towns.ship_cost</c>; manual p. 28).</summary>
    public int ShipPrice => _tables.Services?.ShipCost ?? 1500;

    /// <summary>
    /// <c>_shipyard</c>: "A ship costs 1500 GP." With party gold ≥ the price the shipwright asks (warning first that he takes
    /// back a ship already bought); yes → gold − the price and a new ship moored on this town's ship square of its exit map.
    /// </summary>
    private void Shipwright()
    {
        _window.Line(GameText.S("p4x.0x400f4"));
        if (Party.Gold < ShipPrice || Site!.ShipX is null)
        {
            _window.Print("\"");
            Prompt = P4TownPrompt.Wait;
            return;
        }
        // s151: the original's lines: "｡ " and, with a ship, "船を買うと" continue the offer's line; the warning starts the
        // second (32, 252), the question the third (32, 268); without a ship the question ends the first line.
        _window.Print(GameText.S("p4x.0x4010d"));
        if (Party.Ship != P4ShipState.None)
        {
            _window.Print(GameText.S("p4x.0x40110"));
            _window.Line(GameText.S("p4x.0x4011b"));
            _window.Line(GameText.S("p4x.0x40139"));
        }
        else
            _window.Print(GameText.S("p4x.0x40139"));
        _stage = Stage.Shipwright;
        IconMenu = "_noyes"; // _shipyard asks with _noyes(104, 300)'s YES / NO box, not _confirm's panel (s138, drawn s140)
        Prompt = P4TownPrompt.Confirm;
    }

    public void Answer(bool yes)
    {
        Require(P4TownPrompt.Confirm);
        if (_screen is not null)
        {
            _yes = yes;
            Resume();
            return;
        }
        _stage = Stage.Menu;
        IconMenu = null;
        // s151: _shipyard then prints "Thank you very much." / "Come again." (_wprcen at (156, 300)) and waits (_spc_wait).
        _window.Line(GameText.S(yes ? "p4x.0x40147" : "p4x.0x40160"));
        Prompt = P4TownPrompt.Wait;
        if (!yes)
            return;
        var s = Site!;
        Party.Gold -= ShipPrice;
        Party.LoseShip();
        (Party.Ship, Party.ShipMap, Party.ShipX, Party.ShipY) = (P4ShipState.Moored, s.ExitMap, s.ShipX!.Value, s.ShipY!.Value);
    }

    /// <summary>
    /// <c>_exittown</c>: <c>_par</c>+0A, each member's +3B and <c>_par</c>+0B (<see cref="P4TownRules.Leave"/>); the re-equip is
    /// computed where it is read; the party goes to the town's exit square on the outdoor map; <c>__daytime</c> = 0.
    /// </summary>
    private void Leave()
    {
        var s = Site!;
        P4TownRules.Leave(Party);
        Party.Daytime = 0;
        Transfer = new P4Transfer(P4Transfer.Outdoors, s.ExitMap, s.ExitX, s.ExitY, 0);
        _stage = Stage.Stopped;
        Prompt = P4TownPrompt.Left;
    }

    public void Continue()
    {
        Require(P4TownPrompt.Wait);
        GateRows = null;
        InnRows = null;
        if (_screen is not null)
        {
            Resume();
            return;
        }
        Prompt = P4TownPrompt.Menu;
        Run();
    }

    private void Require(P4TownPrompt prompt)
    {
        if (Prompt != prompt)
            throw new InvalidOperationException($"The town is waiting for {Prompt}, not {prompt}.");
    }

    // --- Saves --------------------------------------------------------------------------------------------------------

    /// <exception cref="InvalidOperationException">A building is open: the original saves only from the town menu.</exception>
    public P4TownSnapshot Capture()
    {
        if (_screen is not null)
            throw new InvalidOperationException("A building is open; leave it before saving.");
        return new(Town, Prompt, [.. _window.Lines], SharesFor, Transfer, _stage.ToString(), []);
    }

    public void Restore(P4TownSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Town = s.Town;
        Prompt = s.Prompt;
        _scene = P4TownScene.None; // past the entry the menu shows the street (Scene); during it, nothing
        _window.Restore(s.Box);
        GateRows = null; // a save at the aging wait reloads its lines in the box only [~]
        InnRows = null;
        BankWindow = null;
        EndingBlock = null;
        SharesFor = s.SharesFor;
        Transfer = s.Transfer;
        _stage = Enum.Parse<Stage>(s.Stage);
        IconMenu = _stage == Stage.Shipwright && Prompt == P4TownPrompt.Confirm ? "_noyes" : null;
        // A save made at the final screen (_endstory → _disk(1)) holds Pendragon (_endset's _tload(1)); loading it starts
        // _game in _town with _cmovf = 2, so _town's Pendragon test runs _ending again with the flags the last ending left:
        // 125 / 150 / 175 set f0–f3, so the reload runs the 200 ending; 200 and 225 run again (ending.md §1.1) [C].
        if (s.Prompt == P4TownPrompt.Ending)
        {
            Prompt = P4TownPrompt.Menu;
            _stage = Stage.Menu;
            Load();
        }
    }
}

/// <summary>Everything a <see cref="P4TownLoop"/> needs to carry on where it stopped (the shop's stock lives in the party).</summary>
public sealed record P4TownSnapshot(
    int Town, P4TownPrompt Prompt, string[] Box, P4ShareKind SharesFor, P4Transfer? Transfer, string Stage, int[] ShopExtras);