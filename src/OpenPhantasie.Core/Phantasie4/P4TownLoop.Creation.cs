namespace OpenPhantasie.Core;

/// <summary>
/// A person on a creation screen (<c>_dsp_buf</c>): name (empty until named; shown "?"), race, class, the five abilities
/// and the move base the screen prints under "Luck" (<c>ancestry.md</c> §5.4).
/// </summary>
public sealed record P4CreationSheet(string Name, int Race, int Class, IReadOnlyList<int> Abilities, int MoveBase);

/// <summary>
/// Guild roster N (<c>_make</c>, <c>ancestry.md</c> §10): plain characters and descendants of two ancestors, made one after
/// another until E or a full roster. The rules are <see cref="P4Creation"/> and <see cref="P4CreationRecord"/>; this file
/// runs the screens in the code's order, so the random draws come in the original's order.
/// </summary>
public sealed partial class P4TownLoop
{
    /// <summary>The person the open creation screen shows in its main window (the character or the descendant).</summary>
    public P4CreationSheet? CreationSheet { get; private set; }

    /// <summary>Ancestor 1 while a descendant is being made ("First ancestor" window).</summary>
    public P4CreationSheet? FirstAncestorSheet { get; private set; }

    /// <summary>Ancestor 2 ("Second ancestor" window); its race alone while only the race is known.</summary>
    public P4CreationSheet? SecondAncestorSheet { get; private set; }

    /// <summary>
    /// The Hall descendant count of <paramref name="h"/> in this game (<c>fames</c> +18): the shipped count until a
    /// character is made with that person as an ancestor.
    /// </summary>
    public int HallDescendants(P4HallPerson h)
    {
        ArgumentNullException.ThrowIfNull(h);
        return h.Slot is >= 0 and < P4Party.HallSlots && Party.HallDescendants[h.Slot] is int n ? n : h.Descendants;
    }

    private P4CreationTables C => _tables.Creation;

    private P4CreationSheet? _work;

    private void ClearCreation()
    {
        CreationSheet = FirstAncestorSheet = SecondAncestorSheet = null;
        _work = null;
    }

    /// <summary><c>_make</c>: the loop of the roster's N button.</summary>
    private IEnumerable<P4TownPrompt> Make()
    {
        var title = ScreenTitle;
        try
        {
            while (true)
            {
                ClearCreation();
                ScreenTitle = title;
                _window.Clear();
                var slot = Array.FindIndex(Party.Roster, m => m is null); // _mkpos
                if (slot < 0)
                {
                    _window.Line(GameText.S("p4x.0x42935"));
                    _window.Line(GameText.S("p4x.0x42946"));
                    yield return P4TownPrompt.Wait;
                    yield break;
                }
                _window.Line(GameText.S("p4x.0x428fe"));
                _window.Line(GameText.S("p4x.0x42911"));
                _window.Line(GameText.S("p4x.0x42926"));
                // The icons E: Finish creating, Yes, No.
                yield return Pick([
                    new(GameText.S("p4x.0x4289e")), new(GameText.S("core.p4.yes")), new(GameText.S("core.p4.no")),
                ], canBack: false, menu: "_makemenu");
                if (_choice == 0)
                    yield break;
                var each = _choice == 1 ? MakeDescendant(slot) : MakePlain(slot);
                foreach (var p in each)
                    yield return p;
            }
        }
        finally
        {
            ClearCreation();
            _window.Clear();
            ScreenTitle = title;
        }
    }

    /// <summary><c>_make2</c>: race, class, the rolled abilities, Okay / Cansel, the name, the record.</summary>
    private IEnumerable<P4TownPrompt> MakePlain(int slot)
    {
        ScreenTitle = GameText.S("p4x.0x42bd5");
        foreach (var p in ChoosePerson(forAncestorOne: false))
            yield return p;
        if (_work is null)
            yield break;
        CreationSheet = _work;
        _window.Clear();
        yield return P4TownPrompt.Confirm;
        if (!_yes)
            yield break;
        foreach (var p in NameAndRecord(slot, CreationSheet, [], [P4AncestorRef.None, P4AncestorRef.None]))
            yield return p;
    }

    /// <summary>
    /// <c>_mkcha</c> up to the sheet: the race menu (Back leaves with <see cref="_work"/> null), Random Creature's list, the
    /// class menu and the new person's abilities (k = 0.6).
    /// </summary>
    private IEnumerable<P4TownPrompt> ChoosePerson(bool forAncestorOne)
    {
        _work = null;
        var names = Data.Names;
        _window.Clear();
        _window.Line(GameText.S("p4x.0x42b7c"));
        _window.Line(GameText.S("p4x.0x42b81"));
        _window.Line(GameText.S("p4x.0x42b8e"));
        // s151: _racemenu draws the seven lines as numbered buttons in window 34 (the Command panel's); the Client too.
        string[] raceLines =
        [
            .. Enumerable.Range(0, P4Ids.BaseRaces).Select(r => P4Names.At(names.Races, r)),
            GameText.S("p4x.0x42bc0"), GameText.S("p4x.0x42bd0"),
        ];
        yield return Pick(raceLines.Select(t => new P4Choice(t)), canBack: false,
            list: P4ListWindow.Of("_racemenu", raceLines));
        if (_choice == P4Ids.BaseRaces + 1)
            yield break;
        var race = _choice;
        if (race == P4Ids.BaseRaces)
        {
            // _rrace: the guild test looks at every used slot for a Hill Giant or Gargoyle (not for ancestor 1).
            var giant = Party.Roster.Any(m => m is not null && (m.Race == P4Ids.HillGiant || m.Race == C.GargoyleRace));
            var list = P4Creation.RandomRaceList(C, _rng, forAncestorOne, giant);
            var randomLines = list.Select(r => P4Names.At(names.Races, r)).ToList();
            yield return Pick(randomLines.Select(t => new P4Choice(t)), canBack: false,
                list: P4ListWindow.Of("_rracemenu", randomLines));
            race = list[_choice];
        }
        foreach (var p in ChooseClass(race))
            yield return p;
        var cls = _choice;
        _work = new P4CreationSheet("", race, cls, P4Creation.RollNewPerson(C, _rng, race, cls),
            P4Creation.Stride(_options, C, race, cls));
    }

    /// <summary><c>_classmenu</c> / <c>_rclass</c>: the race's classes in class order; the answer is left in <see cref="_choice"/> as the class.</summary>
    private IEnumerable<P4TownPrompt> ChooseClass(int race)
    {
        var classes = Enumerable.Range(0, P4Ids.PlayerClasses).Where(c => P4Creation.ClassAllowed(_options, C, race, c)).ToList();
        _window.Clear();
        _window.Line(GameText.S("p4x.0x42b9d"));
        _window.Line(GameText.S("p4x.0x42ba2"));
        _window.Line(GameText.S("p4x.0x42bb1"));
        var classLines = classes.Select(c => P4Names.At(Data.Names.Classes, c)).ToList();
        yield return Pick(classLines.Select(t => new P4Choice(t)), canBack: false,
            list: P4ListWindow.Of("_classmenu", classLines));
        _choice = classes[_choice];
    }

    /// <summary>
    /// <c>_set_fame_lst</c>: the used Hall slots, lettered A, B, …, each "name Rac"; the answer is left in <see cref="_choice"/>
    /// as the index into <see cref="HallPersons"/>.
    /// </summary>
    private P4TownPrompt HallList(string first, string second)
    {
        _window.Clear();
        _window.Line(GameText.S(first));
        _window.Line(GameText.S(second));
        var races = Data.Names.Races;
        string Short(P4HallPerson h)
        {
            var race = P4Names.At(races, h.Race);
            return race.Length > 3 ? race[..3] : race;
        }
        // s151: _set_fame_lst draws them as lettered buttons in window 34 under "#  Name  Race"; the Client too.
        return Pick(HallPersons.Select((h, i) => new P4Choice($"{(char)('A' + i)} {h.Name,-10} {Short(h)}")), canBack: false,
            list: new P4ListWindow("_set_fame_lst", [.. HallPersons.Select((h, i) => new P4ListRow(h.Name, i, Short(h)))], []));
    }

    private static P4Ancestor HallAncestor(P4HallPerson h) =>
        new(h.Race, h.Class, [.. h.Stats.Take(5)], h.Slot, h.Social, h.Results);

    private P4CreationSheet HallSheet(P4HallPerson h) =>
        new(h.Name, h.Race, h.Class, [.. h.Stats.Take(5)],
            h.Class < P4Ids.PlayerClasses && h.Race < C.StrideRace.Count ? P4Creation.Stride(_options, C, h.Race, h.Class) : 0);

    /// <summary><c>_make1</c> → <c>_make5</c>: two ancestors, the descendant's race, class and inherited abilities.</summary>
    private IEnumerable<P4TownPrompt> MakeDescendant(int slot)
    {
        while (true)
        {
            FirstAncestorSheet = SecondAncestorSheet = CreationSheet = null;
            ScreenTitle = GameText.S("p4x.0x42a0b");
            _window.Clear();
            _window.Line(GameText.S("p4x.0x429c8"));
            _window.Line(GameText.S("p4x.0x429d3"));
            _window.Line(GameText.S("p4x.0x429e2"));
            // SPACE (back to the question), N: Decide anew, H: A person from the Hall.
            yield return Pick([new(GameText.S("p4x.0x42988")), new(GameText.S("p4x.0x429a8"))], menu: "_make1menu");
            if (_choice < 0)
                yield break;
            P4Ancestor first;
            if (_choice == 0)
            {
                foreach (var p in ChoosePerson(forAncestorOne: true))
                    yield return p;
                if (_work is null)
                    continue;
                FirstAncestorSheet = _work;
                _window.Clear();
                yield return P4TownPrompt.Confirm;
                if (!_yes)
                    continue;
                first = new P4Ancestor(_work.Race, _work.Class, _work.Abilities);
            }
            else
            {
                yield return HallList("p4x.0x429f1", "p4x.0x429fc");
                var h = HallPersons[_choice];
                FirstAncestorSheet = HallSheet(h);
                _window.Clear();
                yield return P4TownPrompt.Confirm;
                if (!_yes)
                    continue;
                first = HallAncestor(h);
            }

            // _make5: how ancestor 2 is found.
            ScreenTitle = GameText.S("p4x.0x42d26");
            _window.Clear();
            _window.Line(GameText.S("p4x.0x42cd4"));
            _window.Line(GameText.S("p4x.0x42ce5"));
            yield return Pick([
                new(GameText.S("p4x.0x42c5a")), new(GameText.S("p4x.0x42c7a")), new(GameText.S("p4x.0x42c9a")),
            ], menu: "_make5menu");
            if (_choice < 0)
                continue;
            P4Ancestor second;
            P4SecondAncestorResult result;
            if (_choice < 2)
            {
                var same = _choice == 0;
                result = P4Creation.SecondBySameOrDifferent(_options, C, _rng, first, same);
                for (var k = 0; k < result.RacesShown; k++)
                    Party.Sounds.Note(P4Sounds.AncestorCandidate); // _mopta 0x13ACC: _noise per race drawn
                if (result.NotPossible)
                {
                    foreach (var p in NotPossible())
                        yield return p;
                    continue;
                }
                var raceName = P4Names.At(Data.Names.Races, result.Ancestor2Race);
                _window.Clear();
                if (result.Sorry)
                {
                    _window.Line(GameText.S("p4x.0x42cfd"));
                    _window.Line(GameText.S("p4x.0x42d10"));
                    _window.Line(raceName);
                    var end = GameText.S("p4x.0x42d1f");
                    if (end.Length > 0)
                        _window.Line(end);
                }
                // Otherwise the Second ancestor window shows the race alone (_dsp_race), as SecondAncestorSheet below.
                SecondAncestorSheet = new P4CreationSheet("", result.Ancestor2Race, -1, [], 0);
                yield return P4TownPrompt.Wait;
                foreach (var p in ChooseClass(result.Ancestor2Race))
                    yield return p;
                var cls = _choice;
                var abilities = P4Creation.RollNewPerson(C, _rng, result.Ancestor2Race, cls);
                SecondAncestorSheet = new P4CreationSheet("", result.Ancestor2Race, cls, abilities,
                    P4Creation.Stride(_options, C, result.Ancestor2Race, cls));
                second = new P4Ancestor(result.Ancestor2Race, cls, abilities);
            }
            else
            {
                if (P4Creation.HallRefusedAtOnce(_options, C, first))
                {
                    foreach (var p in NotPossible())
                        yield return p;
                    continue;
                }
                yield return HallList("p4x.0x42cba", "p4x.0x42cc5");
                var h = HallPersons[_choice];
                result = P4Creation.SecondFromHall(_options, C, first, HallAncestor(h));
                if (result.NotPossible)
                {
                    foreach (var p in NotPossible())
                        yield return p;
                    continue;
                }
                SecondAncestorSheet = HallSheet(h);
                second = HallAncestor(h);
            }

            // The descendant: class, inherited abilities, Okay / Cansel.
            var race = result.DescendantRace;
            foreach (var p in ChooseClass(race))
                yield return p;
            var dcls = _choice;
            CreationSheet = new P4CreationSheet("", race, dcls, P4Creation.Inherit(_options, C, _rng, first, second, race, dcls),
                P4Creation.Stride(_options, C, race, dcls));
            _window.Clear();
            yield return P4TownPrompt.Confirm;
            if (!_yes)
                yield break;

            // _set_anc_social: ancestor 1, then 2; a new person gets kind 2, index slot × 2 + n and a social roll by race.
            var refs = new P4AncestorRef[2];
            P4Ancestor[] ancestors = [first, second];
            for (var n = 0; n < 2; n++)
            {
                var a = ancestors[n];
                refs[n] = a.HallSlot is int hs
                    ? new P4AncestorRef(P4AncestorKind.Hall, hs)
                    : new P4AncestorRef(P4AncestorKind.New, slot * 2 + n, P4Creation.RollSocial(_options, C, _rng, a.Race),
                        a.Race, a.Class);
            }
            foreach (var p in NameAndRecord(slot, CreationSheet, ancestors, refs))
                yield return p;
            yield break;
        }
    }

    private IEnumerable<P4TownPrompt> NotPossible()
    {
        _window.Clear();
        _window.Line(GameText.S("p4x.0x42cf2"));
        yield return P4TownPrompt.Wait;
    }

    /// <summary>
    /// <c>_mkname</c> (up to 8 characters), <c>_mkdetail</c> into guild slot <paramref name="slot"/>, then the sheet with
    /// the skills until a key.
    /// </summary>
    private IEnumerable<P4TownPrompt> NameAndRecord(int slot, P4CreationSheet sheet, IReadOnlyList<P4Ancestor> ancestors,
        P4AncestorRef[] refs)
    {
        ScreenTitle = GameText.S("p4x.0x42c1c");
        _window.Clear();
        TextLimit = 8;
        yield return P4TownPrompt.Text;
        var name = _text;
        // ancestry.md §7 item 10: the code takes the aging factor from guild slot 0's race (the new character itself when
        // it fills slot 0). Kept with the code's lifespans (D4-033 x68000); the manual's lifespans use the own race.
        var agingRace = _options.P4Code(P4RuleOptions.Lifespans) && slot != 0 && Party.Roster[0] is { } first0
            ? first0.Race
            : sheet.Race;
        var c = P4CreationRecord.Create(_options, _tables, _rng, name, sheet.Race, sheet.Class, sheet.Abilities, ancestors,
            Town, agingRace);
        var m = P4Member.From(c);
        m.Town = Town;
        m.GuildSlot = slot;
        m.Ancestors = refs;
        Party.Roster[slot] = m;
        foreach (var hs in c.HallAncestorSlots)
        {
            var h = HallPersons.FirstOrDefault(x => x.Slot == hs);
            var count = h is null ? Party.HallDescendants[hs] ?? 0 : HallDescendants(h);
            if (count + 1 < 100) // _mkdetail: +1 while it stays below 100
                Party.HallDescendants[hs] = count + 1;
        }
        CreationSheet = sheet with { Name = name };
        Profile = m;
        _window.Clear();
        yield return P4TownPrompt.Wait;
        Profile = null;
    }
}
