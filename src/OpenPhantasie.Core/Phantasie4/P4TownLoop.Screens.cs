namespace OpenPhantasie.Core;

/// <summary>One line of a <see cref="P4TownPrompt.Pick"/> list: its text and whether it can be chosen (a greyed button).</summary>
/// <summary>
/// One line of a pick list. <paramref name="Item"/>: the item the original lists on this row with its icon (<c>_itemlist</c>,
/// <c>_tritmlst</c>: font 2 glyph <c>_idat</c> b0 + 0x20, FORMAT.md section 6.9 "Item lists"), 0 = none.
/// </summary>
public sealed record P4Choice(string Text, bool Enabled = true, int Item = 0);

/// <summary>The town menu's own item commands (T, D); F, C, U and S are the party menus of every place.</summary>
public enum P4TownCommand { Trade, Distribute }

/// <summary>A Hall of Glory person as the Hall shows him (<c>_fame</c>, <c>FORMAT.md</c> §6.3).</summary>
public sealed record P4HallPerson(
    int Slot, string Name, int Race, int Class, int Level, int Social, int Origin, IReadOnlyList<int> Stats,
    IReadOnlyList<int> Results, int Descendants, int DeedsLine, bool Alive);

/// <summary>IV's names by number (<c>names.json</c>): races, classes, social classes, origins, spells.</summary>
public sealed record P4Names(
    IReadOnlyList<string> Races, IReadOnlyList<string> Classes, IReadOnlyList<string> Socials,
    IReadOnlyList<string> Origins, IReadOnlyList<string> Spells, IReadOnlyList<string> Skills)
{
    public static string At(IReadOnlyList<string> list, int i) => i >= 0 && i < list.Count ? list[i] : $"#{i}";
}

/// <summary>What the town buildings and the menus read beyond <see cref="P4Tables"/>: items, names and the Hall.</summary>
public sealed record P4ServiceData(P4ItemCatalog Items, P4Names Names, IReadOnlyList<P4HallPerson> Hall);

/// <summary>
/// The town's buildings and item commands (s102; <c>towns.md</c> §3, §5, §6, §9, <c>training.md</c> §4.1–§5, <c>ending.md</c>):
/// each runs as a coroutine that yields the prompt it waits for (<see cref="P4TownPrompt.Pick"/>, <c>Who</c>, <c>Number</c>,
/// <c>Text</c>, <c>Confirm</c>, <c>Wait</c>, <c>Shares</c>) and reads the answer when resumed. A building cannot be saved
/// mid-way (the original saves only from the town menu's System icon), so <see cref="Capture"/> refuses while one is open.
/// </summary>
public sealed partial class P4TownLoop
{
    private IEnumerator<P4TownPrompt>? _screen;
    private readonly List<P4Choice> _choices = [];
    private int _choice;
    private long _number;
    private string _text = "";
    private bool _yes;
    private bool[] _whoEnabled = [];

    /// <summary>True while a building or item command runs (its prompts are answered here, then the menu returns).</summary>
    public bool InBuilding => _screen is not null;

    /// <summary>The open building's window title.</summary>
    public string? ScreenTitle { get; private set; }

    /// <summary>
    /// The original's Command panel (<c>_mkicon</c>, <c>FORMAT.md</c> 6.9; the Client's <c>x68000-panels.json</c> key) at
    /// the open building prompt when it is not the default one: null = a Wait shows <c>_spc_wait</c>'s, a Confirm
    /// <c>_confirm</c>'s and other prompts none; "" = no panel (the original waits without <c>_spc_wait</c> there). Set by
    /// each screen as it yields (s138), cleared when the screen goes on.
    /// </summary>
    public string? IconMenu { get; private set; }

    /// <summary>
    /// The list the original draws in its Command window at this pick (s151: creation's race / Random Creature / class /
    /// Hall lists, the Magic Training Hall's spell page), or null. Set with the pick, cleared when the screen goes on.
    /// </summary>
    public P4ListWindow? ListWindow { get; private set; }

    /// <summary>
    /// <c>_sklup</c>'s window (s152) while a level-up's skill picks are up: from the first pick through <c>_trang</c>'s
    /// member pick after the third (its rows, the picks left, the last trained skill, done), else null.
    /// </summary>
    public P4SkillWindow? SkillWindow { get; private set; }

    /// <summary>
    /// <c>_trang</c>'s own window (s156) while the Training Hall is open: the member worked on and what was said to him,
    /// from his pick through the next one (the original opens window 32 again at each member pick), else null.
    /// </summary>
    public P4TrainingWindow? TrainingWindow { get; private set; }

    /// <summary>The open <see cref="P4TownPrompt.Pick"/> list.</summary>
    public IReadOnlyList<P4Choice> Choices => _choices;

    /// <summary>Whether SPACE (−1) leaves the open Pick / Who prompt.</summary>
    public bool CanBack { get; private set; } = true;

    /// <summary>Which members the open <see cref="P4TownPrompt.Who"/> prompt accepts.</summary>
    public IReadOnlyList<bool> WhoEnabled => _whoEnabled;

    /// <summary>The highest number the open <see cref="P4TownPrompt.Number"/> prompt takes.</summary>
    public long NumberLimit { get; private set; }

    /// <summary>The longest text the open <see cref="P4TownPrompt.Text"/> prompt takes.</summary>
    public int TextLimit { get; private set; } = 8;

    /// <summary>The member a building works on (the marker <c>_amark</c>), or null.</summary>
    public int? Marked { get; private set; }

    /// <summary>The ending's score once an ending ran (<see cref="P4TownPrompt.Ending"/>).</summary>
    public int? EndingScore { get; private set; }

    private P4ServiceTables S => _tables.Service;

    private P4ServiceData Data => _data ?? throw new InvalidOperationException("IV's item and name data are not loaded.");

    private string ItemName(int id) => _data?.Items.Name(id) ?? $"#{id}";

    // --- The coroutine host --------------------------------------------------------------------------------------------

    private void Open(IEnumerable<P4TownPrompt> screen)
    {
        _window.Clear();
        SkillWindow = null;
        TrainingWindow = null;
        EndingStep = P4EndingStep.None;
        _screen = screen.GetEnumerator();
        Resume();
    }

    private void Resume()
    {
        Prompt = P4TownPrompt.Menu;
        IconMenu = null;
        ListWindow = null;
        var more = _screen!.MoveNext();
        // The Guild treasury collects after every town step that can add gold (sales, creation, Hall joins, level-up
        // gold), before the next prompt shows; idempotent, nothing when the option is off.
        Party.SweepTreasury(_options, inTown: true);
        if (more)
        {
            Prompt = _screen.Current;
            if (Prompt != P4TownPrompt.Ending)
                return;
            // The ending's final screen ends the game: nothing follows, so it can be saved there.
            _screen.Dispose();
            _screen = null;
            return;
        }
        _screen.Dispose();
        _screen = null;
        ScreenTitle = null;
        Marked = null;
        BankWindow = null;
        EndingBlock = null;
        SkillWindow = null;
        TrainingWindow = null;
        _choices.Clear();
        if (Prompt == P4TownPrompt.Menu)
            Run();
    }

    /// <summary>Answers a <see cref="P4TownPrompt.Pick"/> (a line of <see cref="Choices"/>) or a <see cref="P4TownPrompt.Who"/>
    /// (a member); −1 = SPACE / Leave where <see cref="CanBack"/>.</summary>
    public void Choose(int n)
    {
        if (Prompt == P4TownPrompt.Pick)
        {
            if (n == -1 ? !CanBack : n < 0 || n >= _choices.Count || !_choices[n].Enabled)
                throw new ArgumentOutOfRangeException(nameof(n), $"No choice {n}.");
        }
        else if (Prompt == P4TownPrompt.Who)
        {
            if (n == -1 ? !CanBack : n < 0 || n >= _whoEnabled.Length || !_whoEnabled[n])
                throw new ArgumentOutOfRangeException(nameof(n), $"No member {n}.");
        }
        else
            throw new InvalidOperationException($"The town is waiting for {Prompt}, not a choice.");
        _choice = n;
        Resume();
    }

    /// <summary>Answers a <see cref="P4TownPrompt.Number"/> (cut to <see cref="NumberLimit"/>).</summary>
    public void EnterNumber(long n)
    {
        Require(P4TownPrompt.Number);
        _number = Math.Clamp(n, 0, NumberLimit);
        Resume();
    }

    /// <summary>Answers a <see cref="P4TownPrompt.Text"/> (cut to <see cref="TextLimit"/>).</summary>
    public void EnterText(string text)
    {
        Require(P4TownPrompt.Text);
        _text = (text ?? "").Length > TextLimit ? text![..TextLimit] : text ?? "";
        Resume();
    }

    private P4TownPrompt Pick(IEnumerable<P4Choice> choices, bool canBack = true, string? menu = null,
        P4ListWindow? list = null)
    {
        _choices.Clear();
        _choices.AddRange(choices);
        CanBack = canBack;
        IconMenu = menu;
        ListWindow = list;
        return P4TownPrompt.Pick;
    }

    private P4TownPrompt Who(Func<P4Member, bool>? enabled = null, bool canBack = true, string? menu = null)
    {
        _whoEnabled = [.. Party.Members.Select(m => enabled?.Invoke(m) ?? true)];
        CanBack = canBack;
        IconMenu = menu;
        return P4TownPrompt.Who;
    }

    /// <summary><paramref name="prompt"/> with the original's panel <paramref name="menu"/> (<see cref="IconMenu"/>).</summary>
    private P4TownPrompt Icons(string menu, P4TownPrompt prompt)
    {
        IconMenu = menu;
        return prompt;
    }

    private P4TownPrompt Number(long limit)
    {
        NumberLimit = limit;
        return P4TownPrompt.Number;
    }

    private string Name(int member) => Party.Members[member].Name;

    // --- Guild (_guild, _mlist, _trang, _mage) -------------------------------------------------------------------------

    private IEnumerable<P4TownPrompt> Guild()
    {
        SyncRoster();
        while (true)
        {
            ScreenTitle = GameText.S("p4x.0x3fdd5");
            _scene = P4TownScene.Guild;
            var party = Party.Members.Count > 0;
            yield return Pick([
                new(GameText.S("p4x.0x3fabc")), new(GameText.S("p4x.0x3fadc"), party), new(GameText.S("p4x.0x3fafc"), party),
                new(GameText.S("p4x.0x3fa9c")),
            ], canBack: false, menu: "_guildmenu");
            _scene = P4TownScene.None; // _mlist, _trang and _mage clear the picture (_clrtdraw)
            switch (_choice)
            {
                case 0:
                    foreach (var p in Roster())
                        yield return p;
                    break;
                case 1:
                    foreach (var p in Training())
                        yield return p;
                    break;
                case 2:
                    foreach (var p in MagicTraining())
                        yield return p;
                    break;
                default:
                    yield break;
            }
        }
    }

    /// <summary><c>_guild</c>'s entry: every party record is copied back into its guild slot (<c>_tranch</c>).</summary>
    private void SyncRoster()
    {
        foreach (var m in Party.Members)
        {
            if (m.GuildSlot >= 0 && m.GuildSlot < Party.Roster.Length)
                Party.Roster[m.GuildSlot] = m.Clone();
        }
    }

    private IEnumerable<P4TownPrompt> Training()
    {
        ScreenTitle = GameText.S("p4x.0x3fd0c");
        var who = 0;
        while (true)
        {
            Marked = who;
            _window.Clear();
            // s156: _wopen(32, …) at each member pick: a level-up's _sklup window (kept through the pick) goes with it.
            SkillWindow = null;
            var m = Party.Members[who];
            var gate = P4Training.Gate(_options, _tables.Training, m.Class, m.Level, m.Score, m.Condition == P4Member.Okay);
            if (gate == P4TrainingGate.PoorCondition)
            {
                TrainingWindow = new(m.Name, P4TrainingNote.PoorCondition, 0, false);
                _window.Line(m.Name + GameText.S("p4x.0x3fdae") + " " + GameText.S("p4x.0x3fdbf"));
            }
            else if (gate == P4TrainingGate.NothingMoreToTeach)
            {
                TrainingWindow = new(m.Name, P4TrainingNote.NothingMoreToTeach, 0, false);
                _window.Line(m.Name + GameText.S("p4x.0x3fd13"));
            }
            else
            {
                var need = P4Training.ExperienceNeeded(_tables.Training, m.Class, m.Abilities, m.Level) - m.Experience;
                if (need > 0)
                {
                    TrainingWindow = new(m.Name, P4TrainingNote.NeedsExperience, need, false);
                    _window.Line(m.Name + GameText.S("p4x.0x3fd34") + " " + need + GameText.S("p4x.0x3fd47"));
                }
                else
                {
                    var cost = P4Training.TrainingCost(_tables.Training, m.Level, m.Abilities[4],
                        P4Town.PriceIndex(_tables.Towns, Town));
                    TrainingWindow = new(m.Name, P4TrainingNote.Cost, cost, false);
                    _window.Line(m.Name + GameText.S("p4x.0x3fd5b") + " " + cost + GameText.S("p4x.0x3fd6e"));
                    _window.Line(GameText.S("p4x.0x3fd8a"));
                    yield return P4TownPrompt.Confirm;
                    if (_yes)
                    {
                        if (!Party.TrySpend(_options, cost, TreasuryEntryKind.Training))
                        {
                            TrainingWindow = TrainingWindow with { ShortOfGold = true };
                            _window.Line(GameText.S("p4x.0x3fd97").Trim());
                        }
                        else
                        {
                            foreach (var p in LevelUp(m))
                                yield return p;
                        }
                    }
                }
            }
            yield return Who(menu: "_trang"); // E: End training = SPACE
            if (_choice < 0)
            {
                TrainingWindow = null; // _clrtdraw, _wclose(32)
                SkillWindow = null;
                yield break;
            }
            who = _choice;
        }
    }

    /// <summary>
    /// <c>_calpow(record, L)</c> from training: the gold, the three skill picks (<c>_sklup</c>: "Training Menu", Now → After,
    /// the same skill again allowed, no cancel), then HP, magic level, spells and the level; <c>_healch</c>.
    /// </summary>
    private IEnumerable<P4TownPrompt> LevelUp(P4Member m)
    {
        if (!P4Training.CanLevelUp(_options, _tables.Training, m.Class, m.Level))
            yield break;
        P4LevelUp.Begin(_options, _tables, m);
        var title = ScreenTitle;
        ScreenTitle = GameText.S("p4x.0x4168b");
        string? trained = null;
        for (var left = P4Training.SkillPicksPerLevel; left > 0; left--)
        {
            var prompt = GameText.S("p4x.0x416b6") + left + GameText.S("p4x.0x416c2");
            _window.Line(prompt);
            // s152: the Client draws _sklup's window 32 (rows, the prompt at (16, 2), the last trained line at (16, 224)).
            SkillWindow = SkillUpWindow(m, left, prompt, trained, done: false);
            yield return Pick(Enumerable.Range(0, P4Ids.Skills).Select(k =>
                new P4Choice($"{SkillName(k),-11}{m.Skills[k],4} -> {P4LevelUp.SkillAfter(_tables, m, (P4Skill)k),3}")),
                canBack: false);
            P4LevelUp.Pick(_tables, m, (P4Skill)_choice);
            trained = SkillName(_choice) + GameText.S("p4x.0x416dc");
            _window.Line(trained);
        }
        // _sklup's end: the prompt line cleared, the rows as raised (before Finish: _calpow raises HP and the rest after),
        // the bar grey with "This training session is over." s156: as the original, no wait follows (_calpow / _healch
        // call no _spc_wait): _trang goes straight on to its member pick with the window still up, and the window the
        // next pick opens clears it.
        SkillWindow = SkillUpWindow(m, 0, null, trained, done: true);
        P4LevelUp.Finish(_options, _tables, _rng, m);
        _window.Line(GameText.S("p4x.0x416f3"));
        ScreenTitle = title;
    }

    /// <summary>
    /// <c>_sklupmenu</c> / <c>_sklpr</c>'s rows for <paramref name="m"/> now: every skill in skill order, its value and the
    /// value one more pick gives (<c>_calsk</c>), the choice = the skill's index.
    /// </summary>
    private P4SkillWindow SkillUpWindow(P4Member m, int left, string? prompt, string? trained, bool done) =>
        new([.. Enumerable.Range(0, P4Ids.Skills).Select(k =>
            new P4SkillRow(SkillName(k), m.Skills[k], P4LevelUp.SkillAfter(_tables, m, (P4Skill)k), k))],
            left, prompt, trained, done);

    private string SkillName(int k) => P4Names.At(Data.Names.Skills, k);

    private IEnumerable<P4TownPrompt> MagicTraining()
    {
        ScreenTitle = GameText.S("p4x.0x3fc1c");
        var who = 0;
        while (true)
        {
            Marked = who;
            _window.Clear();
            var m = Party.Members[who];
            var known = m.Spells.Count(s => s == 1);
            var open = m.Spells.Count(s => s >= 2);
            // s165: _wopen(32, 160, 32, 312, 352, 1, "魔法訓練場") at each member pick (0x3A96): the name, then one note
            // (FORMAT.md 6.9 "_mage's own window"); the Client draws it as the training hall's.
            const string hall = P4TrainingWindow.MagicHall;
            if (m.Condition != P4Member.Okay)
            {
                TrainingWindow = new(m.Name, P4TrainingNote.PoorCondition, 0, false, hall);
                _window.Line(m.Name + GameText.S("p4x.0x3fce5") + " " + GameText.S("p4x.0x3fcf6"));
            }
            else if (!P4Training.MayLearnMore(_tables.Training, m.Class, m.Level, m.Abilities[1], known, open))
            {
                // The refusal (0x3584): Score (+09) >= 125 (0x35A8) or level (+08) > 30 (0x35D4) -> "nothing more", else
                // "not yet worthy" at (16, 32).
                var nothing = m.Score >= _tables.Training.ScoreGate
                    || m.Level > P4Training.MaxLevel(_options, _tables.Training, m.Class);
                TrainingWindow = new(m.Name, nothing ? P4TrainingNote.NothingMoreToTeach : P4TrainingNote.NotWorthy, 0, false, hall);
                _window.Line(nothing ? m.Name + GameText.S("p4x.0x3fc27") : GameText.S("p4x.0x3fc48"));
            }
            else
            {
                var price = P4Training.SpellPrice(_tables.Training, m.Class, m.Level, m.Abilities[1], m.Abilities[4],
                    P4Town.PriceIndex(_tables.Towns, Town));
                // The offer is printed again before every spell list (0x36F0); the last pick's result row stays under it.
                TrainingWindow = new(m.Name, P4TrainingNote.Offer, price, false, hall);
                var page = 0;
                while (m.Spells.Any(s => s == 2))
                {
                    _window.Clear();
                    _window.Line(GameText.S("p4x.0x3fc6b") + GameText.S("p4x.0x3fc76") + " " + price + GameText.S("p4x.0x3fc81"));
                    _window.Line(GameText.S("p4x.0x3fc97"));
                    // _getmagic(member, 8): five pages of 13 in number order, only open (state 2) spells; page buttons, Quit.
                    var size = S.MagicHallPage;
                    var spells = Enumerable.Range(page * size + 1, size).Where(n => n <= P4Member.SpellCount && m.Spells[n - 1] == 2)
                        .ToList();
                    var pages = (P4Member.SpellCount + size - 1) / size;
                    // s151: the Client draws _getmagic's window in the Command strip: the page buttons 1–5 and 6 Quit on
                    // top, then every spell of the page lettered A…, the name and button only for open ones (_splist).
                    var slots = Enumerable.Range(page * size + 1, size).Where(n => n <= P4Member.SpellCount)
                        .Select(n => spells.IndexOf(n) is >= 0 and var i
                            ? new P4ListRow(P4Names.At(Data.Names.Spells, n - 1), i) : new P4ListRow(null, null));
                    yield return Pick(spells.Select(n => new P4Choice(P4Names.At(Data.Names.Spells, n - 1)))
                        .Concat(Enumerable.Range(0, pages).Select(p => new P4Choice(GameText.S("p4x.0x" + (0x44c06 + 10 * p).ToString("x")))))
                        .Append(new P4Choice(GameText.S("p4x.0x44c38"))), canBack: true,
                        list: new P4ListWindow("_getmagic.8", [.. slots], [.. Enumerable.Range(spells.Count, pages + 1)]));
                    if (_choice < 0 || _choice == spells.Count + pages)
                        break;
                    if (_choice >= spells.Count)
                    {
                        page = _choice - spells.Count;
                        continue;
                    }
                    var spell = spells[_choice];
                    if (!Party.TrySpend(_options, price, TreasuryEntryKind.Spells))
                    {
                        TrainingWindow = TrainingWindow with { ShortOfGold = true, Result = null };
                        _window.Line(GameText.S("p4x.0x3fcb4"));
                        break;
                    }
                    m.AgeUnits = Math.Min(P4Aging.AgeCapFor(_options), m.AgeUnits + S.MagicTrainingAgeUnits);
                    if (P4Training.LessonSucceeds(_rng, m.Abilities[1]))
                    {
                        m.Spells[spell - 1] = 1;
                        TrainingWindow = TrainingWindow with { Result = P4LessonResult.Learned };
                        _window.Line(m.Name + GameText.S("p4x.0x3fcd2"));
                    }
                    else
                    {
                        TrainingWindow = TrainingWindow with { Result = P4LessonResult.Failed };
                        _window.Line(GameText.S("p4x.0x3fcc7"));
                    }
                    yield return Icons("", P4TownPrompt.Wait); // _mage calls no _spc_wait
                }
            }
            yield return Who(menu: "_mage"); // E: End training = SPACE
            if (_choice < 0)
            {
                TrainingWindow = null; // _clrtdraw, _wclose(32)
                yield break;
            }
            who = _choice;
        }
    }

    // --- Guild roster (_mlist, _party, _dislodge, _re_name, _inspect(0)) -----------------------------------------------

    /// <summary>The roster lines: "#, name, race, current town" (<c>_set_mem_lst</c>) for every used slot.</summary>
    public IReadOnlyList<(int Slot, P4Member Member)> RosterList =>
        [.. Party.Roster.Select((m, i) => (i, m)).Where(x => x.m is not null).Select(x => (x.i, x.m!))];

    private IEnumerable<P4TownPrompt> Roster()
    {
        var title = ScreenTitle;
        while (true)
        {
            ScreenTitle = GameText.S("p4x.0x4050d");
            var any = Party.Roster.Any(m => m is not null);
            yield return Pick([
                new(GameText.S("p4x.0x402c2"), any), new(GameText.S("p4x.0x402e2")),
                new(GameText.S("p4x.0x40302"), any), new(GameText.S("p4x.0x40322"), any), new(GameText.S("p4x.0x40342"), any),
            ], menu: "_mlistmenu");
            var c = _choice;
            if (c < 0)
                break;
            if (c is 0 or 3 or 4)
                ClearParty();
            var each = c switch { 0 => FormParty(), 1 => Make(), 2 => RosterProfile(), 3 => Delete(), _ => Rename() };
            foreach (var p in each)
                yield return p;
        }
        ScreenTitle = title;
    }

    /// <summary><c>_clrparty</c>: party size 0, <c>_par</c>+0B = 0 (the members stay in the roster).</summary>
    private void ClearParty()
    {
        SyncRoster();
        Party.Members.Clear();
        Party.AverageLevel = 0;
    }

    private P4TownPrompt RosterPick(string promptId)
    {
        _window.Clear();
        _window.Line(GameText.S(promptId));
        return Pick(RosterList.Select(r => new P4Choice(RosterLine(r.Slot, r.Member))), menu: "_mlistsubset");
    }

    /// <summary>One roster line: slot number, name, race and the current town (<c>_set_mem_lst</c>).</summary>
    public string RosterLine(int slot, P4Member m) =>
        $"{slot + 1,2} {m.Name,-10} {P4Names.At(Data.Names.Races, m.Race),-12} "
        + (m.Town is >= 1 and <= 21 ? GameText.S("p4x.0x" + TownLabel(m.Town).ToString("x")) : "");

    private static int TownLabel(int town) =>
        new[] { 0x40487, 0x40491, 0x4049a, 0x4049f, 0x404a4, 0x404ab, 0x404b3, 0x404b9, 0x404c1, 0x404c7, 0x404cc, 0x404d3,
            0x404dc, 0x404e0, 0x404e6, 0x404eb, 0x404f2, 0x404f5, 0x404fa, 0x40500, 0x40507 }[town - 1];

    private IEnumerable<P4TownPrompt> Delete()
    {
        while (RosterList.Count > 0)
        {
            yield return RosterPick("p4x.0x4041a");
            if (_choice < 0)
                yield break;
            var slot = RosterList[_choice].Slot;
            yield return P4TownPrompt.Confirm;
            if (_yes)
                Party.Roster[slot] = null;
        }
    }

    private IEnumerable<P4TownPrompt> Rename()
    {
        while (true)
        {
            yield return RosterPick("p4x.0x40435");
            if (_choice < 0)
                yield break;
            var m = Party.Roster[RosterList[_choice].Slot]!;
            _window.Line(m.Name + GameText.S("p4x.0x40534"));
            TextLimit = 8;
            yield return P4TownPrompt.Text;
            if (_text.Length == 0)
                continue;
            yield return P4TownPrompt.Confirm;
            if (_yes)
                m.Name = _text;
        }
    }

    /// <summary>The roster member whose profile the Client shows (<c>_inspect(0)</c> → <c>_detail</c>).</summary>
    public P4Member? Profile { get; private set; }

    private IEnumerable<P4TownPrompt> RosterProfile()
    {
        while (true)
        {
            Profile = null;
            yield return RosterPick("p4x.0x4044c");
            if (_choice < 0)
                yield break;
            Profile = Party.Roster[RosterList[_choice].Slot];
            yield return Icons("_inspect", P4TownPrompt.Wait); // "Character info": SPACE
        }
    }

    /// <summary>The slots picked so far in <see cref="FormParty"/>, in party order.</summary>
    public IReadOnlyList<int> Picked => _picked;

    private readonly List<int> _picked = [];

    /// <summary>
    /// <c>_party</c>: toggle up to six roster slots (the order picked is the party order); SPACE ends; confirm; the elemental
    /// (Earth / Mist / Air / none); then the party is built from the guild records, its strategy reset, and members from
    /// another town age 4/26 year.
    /// </summary>
    private IEnumerable<P4TownPrompt> FormParty()
    {
        _picked.Clear();
        while (_picked.Count < S.PartyMax)
        {
            yield return RosterPick("p4x.0x40465");
            if (_choice < 0)
                break;
            var slot = RosterList[_choice].Slot;
            if (!_picked.Remove(slot))
                _picked.Add(slot);
        }
        if (_picked.Count == 0)
            yield break;
        yield return P4TownPrompt.Confirm;
        if (!_yes)
        {
            _picked.Clear();
            yield break;
        }
        _window.Clear();
        _window.Line(GameText.S("p4x.0x40560"));
        yield return Pick([
            new(GameText.S("p4x.0x409aa")), new(GameText.S("p4x.0x409b1")), new(GameText.S("p4x.0x409b8")),
            new(GameText.S("p4x.0x409c1")),
        ], canBack: false, menu: "_party"); // "Elemental": SPACE takes the line
        Party.Elemental = _choice < S.ElementalChoices - 1 ? _choice : null;
        _window.Clear(); // the "Elemental" question does not stay on the roster / town screen
        Party.Members.Clear();
        foreach (var slot in _picked)
        {
            var g = Party.Roster[slot]!;
            if (g.Town != Town)
                g.AgeUnits = Math.Min(P4Aging.AgeCapFor(_options), g.AgeUnits + S.JoinAgeUnits);
            g.Town = Town;
            g.GuildSlot = slot;
            Party.Members.Add(g.Clone());
        }
        Party.StoryFlags[P4Party.AttitudeFlag] = (byte)S.PartyStrategyReset[0];
        Party.StoryFlags[P4Party.FleeFlag] = (byte)S.PartyStrategyReset[1];
        _picked.Clear();
    }

    // --- Bank (_bank, _deposit, _withdraw) ---------------------------------------------------------------------------

    /// <summary>
    /// s163: what the bank's window shows under its picture (<c>_bank</c>'s account table, <c>_deposit</c>'s shares,
    /// <c>_withdraw</c>'s rows; FORMAT.md 6.9 "Building windows under the picture") while the bank is open; else null.
    /// </summary>
    public P4BankWindow? BankWindow { get; private set; }

    /// <summary>
    /// The Guild treasury's window lines (<see cref="TreasuryReport.Lines"/>) while the bank shows the treasury
    /// (<see cref="P4BankView.Treasury"/>); else null.
    /// </summary>
    public IReadOnlyList<string>? TreasuryLines =>
        BankWindow?.View == P4BankView.Treasury ? TreasuryReport.Lines(Party.Treasury, 0) : null;

    private IEnumerable<P4TownPrompt> Bank()
    {
        ScreenTitle = GameText.S("p4x.0x3fef8");
        if (_options.TreasuryOn)
        {
            // The Guild treasury replaces the accounts (11-guild-treasury.md 5): the balance and the ledger, no deposit,
            // withdraw or share; SPACE leaves.
            _window.Clear();
            BankWindow = new(P4BankView.Treasury, [], null);
            yield return P4TownPrompt.Wait;
            yield break;
        }
        while (true)
        {
            _window.Clear();
            BankWindow = new(P4BankView.Accounts, [], null);
            yield return Pick([
                new(GameText.S("p4x.0x3fe26"), Party.Gold != 0 && Party.Members.Count > 0),
                new(GameText.S("p4x.0x3fe46"), Party.Gold != 0xFFFF),
                new(GameText.S("p4x.0x3fe06")),
            ], canBack: false, menu: "_bank");
            if (_choice == 2)
                yield break;
            if (_choice == 0)
            {
                SharesFor = P4ShareKind.Gold;
                BankWindow = new(P4BankView.Deposit, [], null);
                yield return P4TownPrompt.Shares;
                var accounts = P4Town.Deposit(_options, _tables.Towns, Party.Gold, Party.Members.Select(m => m.Account).ToList(),
                    _shares);
                var per = (long)Math.Truncate(Party.Gold / (0.0001 + _shares.Sum()));
                BankWindow = new(P4BankView.Deposit,
                    [.. Party.Members.Select((m, i) => new P4BankRow(i, m.Name, m.Account, 0, null, _shares[i]))], per);
                for (var i = 0; i < accounts.Length; i++)
                    Party.Members[i].Account = accounts[i];
                Party.Gold = 0;
                _window.Line(GameText.S("p4x.0x3fe99") + per + GameText.S("p4x.0x3fea8"));
                yield return P4TownPrompt.Wait;
                continue;
            }
            // s163: _withdraw prints every member's row (name, balance, limit); only a limit above 0 asks for the amount.
            var rows = new List<P4BankRow>();
            for (var i = 0; i < Party.Members.Count; i++)
            {
                var m = Party.Members[i];
                var (_, after) = P4Town.Withdraw(_options, _tables.Towns, m.Account, Party.Gold, long.MaxValue);
                var limit = after - Party.Gold;
                rows.Add(new(i, m.Name, m.Account, Math.Max(0, limit), null, null));
                BankWindow = new(P4BankView.Withdraw, [.. rows], null);
                if (limit <= 0)
                    continue;
                Marked = i;
                _window.Clear();
                _window.Line($"{m.Name,-10} {GameText.S("p4x.0x3fee5")} {m.Account}  {GameText.S("p4x.0x3feea")} {limit}");
                yield return Number(limit);
                var before = Party.Gold;
                (m.Account, Party.Gold) = P4Town.Withdraw(_options, _tables.Towns, m.Account, Party.Gold, _number);
                rows[^1] = rows[^1] with { Amount = Party.Gold - before };
                BankWindow = new(P4BankView.Withdraw, [.. rows], null);
            }
            Marked = null;
            yield return P4TownPrompt.Wait;
        }
    }

    private int[] _shares = [];

    // --- Armory (_armory, _itemlist, _buyitem) -----------------------------------------------------------------------

    /// <summary>The shop's current page: item ids (A–F) as listed (<c>_itemlist</c>).</summary>
    public IReadOnlyList<int> ShopPage => _shopPage;

    private readonly List<int> _shopPage = [];
    private int _shopCursor = 1;

    /// <summary>The price the shop asks now for <paramref name="item"/>: a sold-back copy at D4-020's resale price, else the town price.</summary>
    public int ShopPrice(int item)
    {
        var value = Data.Items.Value(item);
        var t = P4Town.PriceIndex(_tables.Towns, Town);
        return Party.GoodsCount[item] > 0 && Party.GoodsTown[item] == Town
            ? P4Town.ResalePrice(_options, _tables.Towns, value, t)
            : P4Town.BuyPrice(value, t);
    }

    /// <summary>The shop line of an item for member <paramref name="who"/>: points (ids 1–84) or "N-U", and the price.</summary>
    public string ShopLine(int item, int who)
    {
        var info = Data.Items[item];
        var m = Party.Members[who];
        var pts = item is >= 1 and <= 84
            ? P4Equipment.CanUse(info, m) ? P4Town.ItemPoints(_options, _tables.Towns, item, info?.Points ?? 0).ToString() : "N-U"
            : "";
        return $"{ItemName(item),-20}{pts,5}{ShopPrice(item),7}";
    }

    private void FillShopPage()
    {
        _shopPage.Clear();
        var start = _shopCursor;
        do
        {
            if (Party.ShopStock[_shopCursor] > 0)
                _shopPage.Add(_shopCursor);
            _shopCursor = _shopCursor >= P4ItemCatalog.LastShopItem ? P4ItemCatalog.FirstShopItem : _shopCursor + 1;
        }
        while (_shopCursor != P4ItemCatalog.FirstShopItem && _shopPage.Count < S.ShopPage && _shopCursor != start);
    }

    private IEnumerable<P4TownPrompt> Armory()
    {
        ScreenTitle = GameText.S("p4x.0x4257b");
        Marked = 0;
        _shopCursor = P4ItemCatalog.FirstShopItem;
        var pageStart = _shopCursor;
        FillShopPage();
        while (true)
        {
            var who = Marked ?? 0;
            var choices = _shopPage.Select((id, i) => new P4Choice($"{(char)('A' + i)} {ShopLine(id, who)}", Item: id)).ToList();
            choices.Add(new(GameText.S("p4x.0x42583")));
            choices.Add(new(GameText.S("p4x.0x42520")));
            choices.AddRange(Party.Members.Select((m, i) => new P4Choice(m.Name, i != who)));
            yield return Pick(choices, canBack: false, menu: "_armory");
            var c = _choice;
            if (c < _shopPage.Count)
            {
                Buy(who, _shopPage[c]);
                _shopCursor = pageStart;
                FillShopPage();
                continue;
            }
            c -= _shopPage.Count;
            if (c == 0)
            {
                pageStart = _shopCursor;
                FillShopPage();
                continue;
            }
            if (c == 1)
                break;
            Marked = c - 2;
            _window.Clear();
        }
        Marked = null;
    }

    /// <summary><c>_buyitem</c>: "can't pay." / "can't carry more than 9." / "&lt;item&gt; bought." — no confirm.</summary>
    private void Buy(int who, int item)
    {
        _window.Clear();
        var m = Party.Members[who];
        var price = ShopPrice(item);
        if (!Party.CanAfford(_options, price))
        {
            _window.Line(GameText.S("p4x.0x42545"));
            return;
        }
        var slot = Array.IndexOf(m.Items, 0);
        if (slot < 0)
        {
            _window.Line(GameText.S("p4x.0x4255a"));
            return;
        }
        m.Items[slot] = item;
        if (item is P4Party.RingA or P4Party.RingB)
            m.ItemCharges[slot] = P4Party.RingCharges;
        Party.TrySpend(_options, price, TreasuryEntryKind.Armory);
        Party.ShopStock[item]--;
        if (Party.GoodsCount[item] > 0)
            Party.GoodsCount[item]--;
        _window.Line(ItemName(item) + GameText.S("p4x.0x4256f"));
    }

    /// <summary><c>_sell</c>: trunc(value / 4) + 1 into party gold (D4-031 cap); the item's saved stock moves to this town.</summary>
    private void Sell(int item)
    {
        if (_options.TreasuryOn) // the Guild treasury banks a sale at once (11-guild-treasury.md 3)
            Party.Treasury.Add(P4Town.SellPrice(Data.Items.Value(item)), TreasuryEntryKind.Sale);
        else
            Party.Gold = P4Town.Cap(_options, _tables.Towns, Party.Gold + P4Town.SellPrice(Data.Items.Value(item)),
                P4Town.GoldPath.Sale);
        Party.GoodsCount[item] = Math.Min(255, Party.GoodsCount[item] + 1);
        Party.GoodsTown[item] = Town;
    }

    // --- Item distribution (_distribute) and exchange (_trade) -------------------------------------------------------

    /// <summary>The item being placed (distribution) or held (exchange), or 0.</summary>
    public int HeldItem { get; private set; }

    /// <summary>The items still to place after <see cref="HeldItem"/> (distribution).</summary>
    public IReadOnlyList<int> Queue => _queue.Select(q => q.Item).ToList();

    private readonly List<(int Item, int Charge)> _queue = [];

    /// <summary>
    /// <c>_distribute(kind)</c>: kind 0 (town "D") takes every member's carried items and the pack; kind 1 (arrival) the pack
    /// only, and nothing happens when the pack is empty or nobody is alive. Highest id first; each item to a member with room,
    /// back to the pack (P) or sold (S).
    /// </summary>
    private IEnumerable<P4TownPrompt> Distribute(bool arrival)
    {
        ScreenTitle = GameText.S("p4x.0x40bd8");
        if (arrival && (Party.Pack.All(i => i == 0) || Party.Members.Count == 0))
            yield break;
        if (!Party.AnyAlive)
            yield break;
        _queue.Clear();
        if (!arrival)
        {
            foreach (var m in Party.Members)
            {
                for (var i = 0; i < P4Member.CarriedSlots; i++)
                {
                    if (m.Items[i] != 0)
                        _queue.Add((m.Items[i], m.ItemCharges[i]));
                    m.Items[i] = 0;
                    m.ItemCharges[i] = 0;
                }
            }
        }
        for (var i = 0; i < P4Party.PackSlots; i++)
        {
            if (Party.Pack[i] != 0)
                _queue.Add((Party.Pack[i], Party.PackCharges[i]));
            Party.Pack[i] = 0;
            Party.PackCharges[i] = 0;
        }
        // _dvsort: a selection sort, highest id first.
        for (var i = 0; i < _queue.Count - 1; i++)
        {
            var best = i;
            for (var j = i; j < _queue.Count; j++)
            {
                if (_queue[j].Item > _queue[best].Item)
                    best = j;
            }
            (_queue[i], _queue[best]) = (_queue[best], _queue[i]);
        }
        var inPack = 0;
        while (_queue.Count > 0)
        {
            var (item, charge) = _queue[0];
            _queue.RemoveAt(0);
            HeldItem = item;
            _window.Clear();
            var users = Party.Members.Select((m, i) => P4Equipment.CanUse(Data.Items[item], m) ? (i + 1).ToString() : " ");
            _window.Line($"{ItemName(item)}  ({string.Concat(users)})");
            var choices = Party.Members.Select(m => new P4Choice(m.Name, Array.IndexOf(m.Items, 0) >= 0)).ToList();
            choices.Add(new(GameText.S("p4x.0x40b6c"), inPack < P4Party.PackSlots));
            choices.Add(new(GameText.S("p4x.0x40b8c")));
            yield return Pick(choices, canBack: false, menu: "_distribute");
            var c = _choice;
            if (c < Party.Members.Count)
            {
                var m = Party.Members[c];
                var slot = Array.IndexOf(m.Items, 0);
                (m.Items[slot], m.ItemCharges[slot]) = (item, charge);
            }
            else if (c == Party.Members.Count)
            {
                (Party.Pack[inPack], Party.PackCharges[inPack]) = (item, charge);
                inPack++;
            }
            else
                Sell(item);
        }
        HeldItem = 0;
        yield return P4TownPrompt.Wait;
    }

    private IEnumerable<P4TownPrompt> Trade()
    {
        ScreenTitle = GameText.S("p4x.0x4278d");
        var who = 0;
        var charge = 0;
        HeldItem = 0;
        while (true)
        {
            Marked = who;
            var m = Party.Members[who];
            var holding = HeldItem != 0;
            var choices = m.Items.Select(i => new P4Choice(i == 0 ? "-" : ItemName(i), !holding && i != 0, i)).ToList();
            choices.AddRange(Party.Members.Select(x =>
                new P4Choice(x.Name, holding ? Array.IndexOf(x.Items, 0) >= 0 : x.Items.Any(i => i != 0))));
            choices.Add(new(GameText.S("p4x.0x42738"), holding && Array.IndexOf(Party.Pack, 0) >= 0));
            choices.Add(new(GameText.S("p4x.0x42758"), holding));
            choices.Add(new(GameText.S("p4x.0x42718"), !holding));
            yield return Pick(choices, canBack: false, menu: "_trade");
            var c = _choice;
            if (c < P4Member.CarriedSlots)
            {
                (HeldItem, charge) = (m.Items[c], m.ItemCharges[c]);
                (m.Items[c], m.ItemCharges[c]) = (0, 0);
                continue;
            }
            c -= P4Member.CarriedSlots;
            if (c < Party.Members.Count)
            {
                if (!holding)
                {
                    who = c;
                    continue;
                }
                var to = Party.Members[c];
                var slot = Array.IndexOf(to.Items, 0);
                (to.Items[slot], to.ItemCharges[slot]) = (HeldItem, charge);
                HeldItem = 0;
                continue;
            }
            c -= Party.Members.Count;
            if (c == 0)
            {
                var slot = Array.IndexOf(Party.Pack, 0);
                (Party.Pack[slot], Party.PackCharges[slot]) = (HeldItem, charge);
                HeldItem = 0;
            }
            else if (c == 1)
            {
                Sell(HeldItem);
                HeldItem = 0;
            }
            else
                break;
        }
        Marked = null;
    }

    /// <summary>Runs the town menu's T (Trade items) or D (Share out items).</summary>
    public void Command(P4TownCommand command)
    {
        Require(P4TownPrompt.Menu);
        if (Party.Members.Count == 0)
            return;
        _scene = P4TownScene.None;
        Open(command == P4TownCommand.Trade ? Trade() : Distribute(arrival: false));
    }

    // --- Hall of Glory (_hall, _hhist, _hprof) -----------------------------------------------------------------------

    /// <summary>The Hall person whose sheet is open (<c>_dsp_hinfo</c>), or null.</summary>
    public P4HallPerson? HallPerson { get; private set; }

    /// <summary>The Hall's history pages (<c>_hstr</c>, nine string ids).</summary>
    public static readonly string[] HistoryPages =
        ["p4x.0x41d1f", "p4x.0x41d8c", "p4x.0x41e03", "p4x.0x41ef8", "p4x.0x41fc7", "p4x.0x420c2", "p4x.0x421b3", "p4x.0x422b6",
            "p4x.0x42357"];

    private IEnumerable<P4TownPrompt> Hall()
    {
        ScreenTitle = GameText.S("p4x.0x424fd");
        while (true)
        {
            HallPerson = null;
            _scene = P4TownScene.Hall;
            _window.Clear();
            yield return Pick([
                new(GameText.S("p4x.0x419a6")), new(GameText.S("p4x.0x419e6")), new(GameText.S("p4x.0x419c6")),
                new(GameText.S("p4x.0x41986")),
            ], canBack: false, menu: "_hall");
            if (_choice == 3)
                yield break;
            if (_choice == 2)
            {
                foreach (var p in HallTransfer())
                    yield return p;
                continue;
            }
            if (_choice == 0)
            {
                _scene = P4TownScene.HallHistory;
                HallHistoryPage = 0;
                _leaveHistory = false;
                foreach (var page in HistoryPages)
                {
                    _window.Clear();
                    _window.Line(GameText.S(page).Trim());
                    // _hhist: pages 1–8 wait on its L / N panel (L leaves at once), the last on _spc_wait.
                    yield return HallHistoryPage < HistoryPages.Length - 1 ? Icons("_hhist", P4TownPrompt.Wait) : P4TownPrompt.Wait;
                    if (_leaveHistory)
                        break;
                    HallHistoryPage++;
                }
                continue;
            }
            while (true)
            {
                HallPerson = null;
                yield return Pick(HallPersons.Select(h => new P4Choice(h.Name)), menu: "_hprofmenu"); // L: Done = SPACE
                if (_choice < 0)
                    break;
                HallPerson = HallPersons[_choice];
                yield return P4TownPrompt.Wait;
            }
        }
    }

    /// <summary>The Hall persons of this game: the shipped three and the III imports (<see cref="P4Hall.Persons"/>).</summary>
    public IReadOnlyList<P4HallPerson> HallPersons => P4Hall.Persons(Data.Hall, Party);

    /// <summary>
    /// Where the Hall's T reads a game's Guild roster (the remaster's save of that game, set by the front end): Phantasie
    /// III, and with <c>p4.transfer-games</c> = <c>all</c> also I and II; null, or a null roster, = no save of that game.
    /// The original reads III's player disk instead (<c>_p3disk</c>).
    /// </summary>
    public Func<GameId, IReadOnlyList<Character?>?>? HallTransferSource { get; set; }

    /// <summary>
    /// The characters the open transfer list shows (<c>_show_gm</c>) and which of them are marked (<c>_mark2</c>); empty
    /// outside the transfer.
    /// </summary>
    public IReadOnlyList<(Character Character, bool Marked)> TransferList => _transferList;

    private readonly List<(Character Character, bool Marked)> _transferList = [];

    /// <summary>The open transfer list's heading: "== Phantasie III ==" (<c>0x4281a</c>), or the I / II one (section 8.4).</summary>
    private string _transferHeading = "";

    /// <summary>
    /// The Hall's T (<c>_htran</c> → <c>_trans</c> → <c>_mtrans</c>, <c>ancestry.md</c> section 8.2): "Transfer Phantasie III's character
    /// data?" (O / C); III's Guild roster in a list, A, B, …; with no free Hall slot "No more can be transferred." and SPACE;
    /// else each pick marks or unmarks a character ("N more can be transferred.") until the marks fill the free slots or O
    /// (X leaves with nothing copied); then Okay / Cansel, and each marked character, in list order, takes the first free
    /// slot. The original's disk prompts (swap to III's player disk and back) have no remaster equivalent: the III save is
    /// read where the original reads the disk; without one the transfer says so and returns.
    /// <para>
    /// T5 options (s149): <c>p4.transfer-games</c> = <c>all</c> first asks which game (I / II / III; I and II into the Hall
    /// only, section 8.4); <c>p4.transfer-join</c> = <c>guild</c> asks for III whether the characters go to the Hall or join
    /// the Guild (section 8.3: Free = the empty Guild slots). Both off: the original's flow, no extra question.
    /// </para>
    /// </summary>
    private IEnumerable<P4TownPrompt> HallTransfer()
    {
        _window.Clear();
        _window.Line(GameText.S("p4x.0x41a06"));
        _window.Line(GameText.S("p4x.0x41a13"));
        _window.Line(GameText.S("p4x.0x41a1f"));
        yield return Icons("_htran", P4TownPrompt.Confirm);
        if (!_yes)
            yield break;
        var source = GameId.P3;
        if (_options.P4TransferFromAllGames())
        {
            _window.Clear();
            _window.Line(GameText.S("core.p4.hall_from_which_game"));
            yield return Pick([
                new(GameText.S("core.p4.hall_game_i")), new(GameText.S("core.p4.hall_game_ii")),
                new(GameText.S("core.p4.hall_game_iii")),
            ], menu: "");
            if (_choice < 0)
                yield break;
            source = _choice switch { 0 => GameId.P1, 1 => GameId.P2, _ => GameId.P3 };
        }
        if (HallTransferSource?.Invoke(source) is not { } roster)
        {
            _window.Clear();
            _window.Line(GameText.S(source switch
            {
                GameId.P1 => "core.p4.hall_no_i_save", GameId.P2 => "core.p4.hall_no_ii_save", _ => "core.p4.hall_no_iii_save",
            }));
            yield return P4TownPrompt.Wait;
            yield break;
        }
        var guild = false;
        if (source == GameId.P3 && _options.P4TransferJoinsGuild())
        {
            _window.Clear();
            _window.Line(GameText.S("core.p4.hall_where"));
            yield return Pick([new(GameText.S("core.p4.hall_into_hall")), new(GameText.S("core.p4.hall_into_guild"))],
                menu: "");
            if (_choice < 0)
                yield break;
            guild = _choice == 1;
        }

        var title = ScreenTitle;
        _scene = P4TownScene.None; // _show_gm: _clrtdraw
        ScreenTitle = GameText.S("p4x.0x42810");
        _transferHeading = source switch
        {
            GameId.P1 => GameText.S("core.p4.hall_list_i"),
            GameId.P2 => GameText.S("core.p4.hall_list_ii"),
            _ => GameText.S("p4x.0x4281a"),
        };
        _transferList.Clear();
        _transferList.AddRange(roster.OfType<Character>().Where(c => !guild || P4Hall.CanJoin(c)).Select(c => (c, false)));
        try
        {
            var free = guild ? Party.Roster.Count(m => m is null) : P4Hall.FreeSlots(Party);
            if (free == 0)
            {
                TransferWindow(0);
                yield return P4TownPrompt.Wait;
                yield break;
            }
            var count = _transferList.Count;
            while (true)
            {
                var marked = _transferList.Count(t => t.Marked);
                TransferWindow(free - marked);
                yield return Pick([.. TransferChoices(), new(GameText.S("p4x.0x42796")), new(GameText.S("p4x.0x427b6"))],
                    canBack: false, menu: "_show_gm");
                if (_choice == count + 1) // X: Abort
                    yield break;
                if (_choice == count) // O: Okay
                    break;
                var (c, m) = _transferList[_choice];
                _transferList[_choice] = (c, !m);
                if (marked + (m ? -1 : 1) >= free)
                {
                    TransferWindow(0);
                    break;
                }
            }
            yield return P4TownPrompt.Confirm;
            if (!_yes)
                yield break;
            foreach (var (c, _) in _transferList.Where(t => t.Marked))
            {
                if (guild)
                {
                    var g = Array.FindIndex(Party.Roster, m => m is null);
                    if (g < 0)
                        break;
                    // As creation (NameAndRecord): the code's lifespans take the aging factor from guild slot 0's race.
                    int? agingRace = _options.P4Code(P4RuleOptions.Lifespans) && !_options.SharedAging && g != 0
                        && Party.Roster[0] is { } first0
                        ? first0.Race : null;
                    Party.Roster[g] = P4Hall.JoinFromPhantasieIII(_options, _tables, _rng, c, g, Town, agingRace);
                    continue;
                }
                var slot = Array.FindIndex(Party.HallImports, P4Hall.FirstImportSlot, h => h is null);
                if (slot < 0)
                    break;
                Party.HallImports[slot] = source == GameId.P3
                    ? P4Hall.FromPhantasieIII(c, slot)
                    : P4Hall.FromEarlierGame(c, source, slot);
            }
        }
        finally
        {
            _transferList.Clear();
            ScreenTitle = title;
            _scene = P4TownScene.Hall;
        }
    }

    /// <summary><c>_show_gm</c>'s heading and <c>_cntdown</c>'s line: "N more can be transferred.", or none left at 0.</summary>
    private void TransferWindow(int left)
    {
        _window.Clear();
        _window.Line(_transferHeading.Trim());
        _window.Line(GameText.S("p4x.0x4282c"));
        _window.Line(left == 0 ? GameText.S("p4x.0x4285a") : $"{left}{GameText.S("p4x.0x42879")}");
    }

    /// <summary>The list lines: letter, name and III's three-letter class (<c>0x427d6</c>), "*" before a marked one.</summary>
    private IEnumerable<P4Choice> TransferChoices()
    {
        string[] classes = ["p4x.0x427ee", "p4x.0x427f2", "p4x.0x427f6", "p4x.0x427fa", "p4x.0x427fe", "p4x.0x42802"];
        return _transferList.Select((t, i) =>
        {
            var letter = i < 26 ? (char)('A' + i) : ' ';
            var cls = (int)t.Character.Class is var k && k < classes.Length ? GameText.S(classes[k]) : "?";
            return new P4Choice($"{(t.Marked ? '*' : ' ')}{letter} {t.Character.Name,-10} {cls}");
        });
    }

    private bool _leaveHistory;

    /// <summary>True while a Hall history page waits on <c>_hhist</c>'s panel, whose L leaves the history (<see cref="LeaveHistory"/>).</summary>
    public bool CanLeaveHistory => Prompt == P4TownPrompt.Wait && IconMenu == "_hhist";

    /// <summary><c>_hhist</c>'s L (Done): leaves the history at once, back to the Hall's menu (SPACE / N turns the page).</summary>
    public void LeaveHistory()
    {
        if (!CanLeaveHistory)
            throw new InvalidOperationException("No Hall history page is open.");
        _leaveHistory = true;
        Resume();
    }

    /// <summary>
    /// The Hall sheet's deeds line pair (<c>_dsp_hinfo</c>, ancestry.md §8): slots 0–2 their stored index; imports from the
    /// per-game results; then the two string ids.
    /// </summary>
    public static (string First, string Second) HallDeeds(P4HallPerson h)
    {
        ArgumentNullException.ThrowIfNull(h);
        int index;
        if (h.Slot < 3)
            index = h.DeedsLine;
        else if (h.Results[2] != 0)
            index = h.Results[2] < 100 ? 9 : h.Social == 5 ? 7 : 8;
        else if (h.Results[1] != 0)
            index = h.Results[1] >= 100 ? 5 : 6;
        else if (h.Results[0] != 0)
            index = h.Results[0] >= 100 ? 3 : 4;
        else
            index = 10;
        string[] first = ["0x41bc8", "0x41bed", "0x41c20", "0x41c35", "0x41c54", "0x41c6a", "0x41c6a", "0x41caa", "0x41ce1",
            "0x41cfe", "0x41d15"];
        string[] second = ["0x41be0", "0x41c06", "", "0x41c45", "", "0x41c81", "0x41c91", "0x41cc5", "", "", ""];
        return ("p4x." + first[index], second[index].Length == 0 ? "" : "p4x." + second[index]);
    }

    // --- Ending (_ending, _endset, _endstory) ------------------------------------------------------------------------

    /// <summary>The ending a Pendragon entry runs, by <see cref="P4TownRules.Ending"/>: its messages, then the final screen.</summary>
    private IEnumerable<P4TownPrompt> Ending(int ending)
    {
        var text = S.EndingText;
        ScreenTitle = GameText.S("p4x.0x413bb");
        var score = P4TownRules.ApplyEnding(S, Party, ending);
        EndingScore = score;
        foreach (var block in text.Sequences[ending])
        {
            // _banquet before its blocks (via _common_proc for the Lord title), _arathamus before the cell block.
            if (text.BanquetBeforeBlocks.Contains(block))
                _scene = P4TownScene.EndingBanquet;
            else if (block == text.CellBeforeBlock)
                _scene = P4TownScene.EndingCell;
            // _endmsg: three lines a page, each page waits for SPACE.
            var lines = text.Messages[block];
            EndingBlock = block; // s163: _endmsg(block)'s window 33 holds the page
            for (var i = 0; i < lines.Count; i += 3)
            {
                _window.Clear();
                foreach (var id in lines.Skip(i).Take(3))
                    _window.Line(GameText.S(id).Trim());
                Party.Sounds.Note(P4Sounds.EndingPage); // _endmsg 0xA922: _sdbeep, then _spc_wait
                yield return P4TownPrompt.Wait;
            }
            if (block != text.LordTitleBlock)
                continue;
            // _common_proc: each member "<origin> of <name> Lord" (the social class became Lord in ApplyEnding).
            foreach (var m in Party.Members)
            {
                _window.Clear();
                _window.Line(P4Names.At(Data.Names.Origins, m.Origin) + GameText.S(text.OfId) + m.Name + GameText.S(text.LordId));
                Party.Sounds.Note(P4Sounds.EndingPage); // _common_proc 0xAFFA: _sdbeep, then _spc_wait
                yield return P4TownPrompt.Wait;
            }
        }
        // _endstory: "Phantasie IV — THE GAME END", the score and the fixed completion code; then the save question.
        // s162: its _wopen(32, …, 1, "Phantasie IV") fills the picture's place grey: no picture from here on.
        _scene = P4TownScene.EndingFinal;
        EndingBlock = null;
        _window.Clear();
        foreach (var id in text.FinalLines)
            _window.Line(GameText.S(id));
        _window.Line(GameText.S(text.ScoreIds[0]) + score + GameText.S(text.ScoreIds[1]));
        _window.Line(GameText.S(text.CodeIds[0]) + GameText.S(text.CodeIds[1]) + GameText.S(text.CodeIds[2]));
        foreach (var id in text.PostcardLines)
            _window.Line(id.Length == 0 ? "" : GameText.S(id)); // 0x41373 is an empty string: a blank row
        yield return P4TownPrompt.Wait; // s158: _spc_wait (0xAB88) before the question, no beep
        // s158: line 8, "<<Save the game?>>" (_font(1, 1, 2, 16)), _sdbeep (0xABFC), _confirm. The question is the final
        // screen's prompt itself, so a save made at it (YES: _disk(1), the shell's save) reloads into the ending again.
        _window.Line(GameText.S(text.SaveQuestion));
        _stage = Stage.Stopped;
        Party.Sounds.Note(P4Sounds.EndingFinal);
        EndingStep = P4EndingStep.Question;
        IconMenu = "_confirm";
        yield return P4TownPrompt.Ending;
    }

    /// <summary>
    /// s158: the final screen's step (<c>_endstory</c>, <c>ending.md</c> §3): <see cref="P4EndingStep.Question"/> while the
    /// save question waits for its answer, <see cref="P4EndingStep.Answered"/> after it (the front end then shows
    /// <c>_acgamend(1)</c>'s End game / Back to title).
    /// </summary>
    public P4EndingStep EndingStep { get; private set; }

    /// <summary>
    /// s163: the <c>_endmsg</c> block whose page waits now (its window 33 holds the page's lines; <c>_common_proc</c>'s Lord
    /// titles go in the same window, so they keep the last block), else null.
    /// </summary>
    public int? EndingBlock { get; private set; }

    /// <summary>
    /// s158: the save question's answer (<c>_endstory</c> <c>0xAC02</c>–<c>0xAC26</c>): a YES saves first (<c>_disk(1)</c>;
    /// in the remaster the front end's save, taken before this call, while the prompt is still the final screen), then on
    /// either answer the second <c>_sdbeep</c> (<c>0xAC1A</c>) and <c>_acgamend(1)</c>.
    /// </summary>
    public void AnswerEnding()
    {
        Require(P4TownPrompt.Ending);
        if (EndingStep != P4EndingStep.Question)
            throw new InvalidOperationException("The final screen's save question is not open.");
        EndingStep = P4EndingStep.Answered;
        IconMenu = null;
        Party.Sounds.Note(P4Sounds.EndingFinal);
    }
}

/// <summary>s163: which of the bank's tables its window shows (<see cref="P4TownLoop.BankWindow"/>).</summary>
public enum P4BankView { Accounts, Deposit, Withdraw, Treasury }

/// <summary>
/// s163: a row of the bank's deposit / withdraw table: party member <see cref="Member"/>, the balance shown
/// (<c>_withdraw</c>: before the withdrawal; deposit: after), the withdraw limit, the amount drawn (null until asked; a
/// limit of 0 asks nothing), the deposit share.
/// </summary>
public sealed record P4BankRow(int Member, string Name, long Account, long Limit, long? Amount, int? Share);

/// <summary>s163: the bank window's view, its rows (deposit / withdraw) and the deposit's amount per share once paid in.</summary>
public sealed record P4BankWindow(P4BankView View, IReadOnlyList<P4BankRow> Rows, long? PerShare);
