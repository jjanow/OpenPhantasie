namespace OpenPhantasie.Core;

/// <summary>What the party menus wait for (<see cref="P4Menus.Prompt"/>).</summary>
public enum P4MenuPrompt
{
    /// <summary>No menu is open: the place's own loop runs.</summary>
    Closed,

    /// <summary>A list (<see cref="P4Menus.Choices"/>; <see cref="P4Menus.Choose"/>, −1 = SPACE where <see cref="P4Menus.CanBack"/>).</summary>
    Pick,

    /// <summary>A member (<see cref="P4Menus.WhoEnabled"/>; <see cref="P4Menus.Choose"/>, −1 = SPACE).</summary>
    Who,

    /// <summary>"SPACE" (<see cref="P4Menus.Continue"/>).</summary>
    Wait,

    /// <summary>Yes / no (<see cref="P4Menus.Answer"/>).</summary>
    Confirm,
}

/// <summary>The party menus every place shows: F Profile, P Party info (outdoors), C Spells, U Use item.</summary>
public enum P4MenuCommand { Profile, PartyInfo, Cast, Use }

/// <summary>
/// Phantasie IV's party menus outside combat (s102): F Profile (<c>_inspect</c> → <c>_detail</c>, pages 1–3, page 2 the
/// combat strategy A–E), P Party info (<c>_party_info</c>: gold, experience pool, items found, party strategy A / B), C
/// Spells (<c>_ocast(place)</c>, <c>_getmagic</c>, <c>_pmagic3</c>; D4-039, D4-051, D4-053, D4-054, D4-061) and U Use item
/// (<c>_use</c>: potions, magic items, scrolls, The Box; D4-035). S System is the shell's (save / load). Specs:
/// <c>towns.md</c> §3, <c>spells-noncombat.md</c>, <c>items.md</c>. Coroutine-driven like the town's buildings; a menu that
/// moves the party (Transportation, The Box) ends with <see cref="Transfer"/> set for the campaign.
/// </summary>
public sealed class P4Menus
{
    private readonly GameOptions _o;
    private readonly P4Tables _t;
    private readonly IRandomSource _rng;
    private readonly P4ServiceData _data;
    private readonly P4MessageWindow _window = new();
    private readonly List<P4Choice> _choices = [];
    private IEnumerator<P4MenuPrompt>? _screen;
    private int _choice;
    private bool _yes;
    private bool[] _whoEnabled = [];

    public P4Menus(GameOptions options, P4Tables tables, P4Party party, IRandomSource rng, P4ServiceData data)
    {
        _o = options ?? throw new ArgumentNullException(nameof(options));
        _t = tables ?? throw new ArgumentNullException(nameof(tables));
        Party = party ?? throw new ArgumentNullException(nameof(party));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public P4Party Party { get; }

    public P4MenuPrompt Prompt { get; private set; } = P4MenuPrompt.Closed;

    public string? Title { get; private set; }

    public IReadOnlyList<string> Box => _window.Lines;

    public IReadOnlyList<P4Choice> Choices => _choices;

    public bool CanBack { get; private set; } = true;

    public IReadOnlyList<bool> WhoEnabled => _whoEnabled;

    /// <summary>
    /// The original's Command panel or button row at the open prompt (s140, FORMAT.md 6.9; the Client's
    /// <c>x68000-panels.json</c> key) when it is not the default one: null = a Wait shows <c>_spc_wait</c>'s, a Confirm
    /// <c>_confirm</c>'s, other prompts none; "" = no panel. F's member pick <c>_inspect</c>, the profile pages'
    /// window-36 rows <c>_detailmenu</c> / <c>_p_info3menu</c> / <c>_page3</c>, P <c>_p_info1menu</c>, C's caster
    /// <c>_ocast</c> and target <c>_ocast.2</c>, Transportation's towns <c>_cast_47</c>, Identify <c>_setident</c>, U
    /// <c>_setuse</c>. Set by each screen as it yields, cleared when it goes on.
    /// </summary>
    public string? IconMenu { get; private set; }

    /// <summary>
    /// The list the original draws in its Command window at this pick (s151: the spell list <c>_getmagic</c>, Transportation's
    /// towns <c>_cast_47</c>), or null. Set with the pick, cleared when it goes on.
    /// </summary>
    public P4ListWindow? ListWindow { get; private set; }

    /// <summary>The member a menu works on (the marker), or null.</summary>
    public int? Marked { get; private set; }

    /// <summary>The open profile: member and page 1–3 (<c>_detail</c>), or null.</summary>
    public (P4Member Member, int Page)? Profile { get; private set; }

    /// <summary>A scroll's pages are showing (<c>_use_sc</c>: the SCBACK picture under the text).</summary>
    public bool ReadingScroll { get; private set; }

    /// <summary>The potion in hand (Use item), or 0.</summary>
    public int HeldItem { get; private set; }

    /// <summary>Where a menu moved the party (Transportation to a town, The Box outdoors); read by the campaign, then cleared.</summary>
    public P4Transfer? Transfer { get; set; }

    private P4ServiceTables S => _t.Service;

    // --- Host -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Opens a menu at <paramref name="place"/>: <paramref name="map"/> is <c>_cmapn</c> (Divine Aid's hint, a dungeon's
    /// Monster Evaluation level), <paramref name="encounterLevel"/> the outdoor <c>_emlevel</c>.
    /// </summary>
    public void Open(P4MenuCommand command, P4CastPlace place, int map, int encounterLevel = 1)
    {
        if (Prompt != P4MenuPrompt.Closed)
            throw new InvalidOperationException("A menu is already open.");
        if (Party.Members.Count == 0)
            return;
        _window.Clear();
        Transfer = null;
        ReadingScroll = false;
        _screen = (command switch
        {
            P4MenuCommand.Profile => ProfileMenu(),
            P4MenuCommand.PartyInfo => PartyInfo(),
            P4MenuCommand.Cast => Cast(place, map, encounterLevel),
            _ => Use(),
        }).GetEnumerator();
        Resume();
    }

    private void Resume()
    {
        IconMenu = null;
        ListWindow = null;
        if (_screen!.MoveNext())
        {
            Prompt = _screen.Current;
            return;
        }
        _screen.Dispose();
        _screen = null;
        Prompt = P4MenuPrompt.Closed;
        Title = null;
        Marked = null;
        Profile = null;
        HeldItem = 0;
        ReadingScroll = false;
        _choices.Clear();
    }

    public void Choose(int n)
    {
        var ok = Prompt switch
        {
            P4MenuPrompt.Pick => n == -1 ? CanBack : n >= 0 && n < _choices.Count && _choices[n].Enabled,
            P4MenuPrompt.Who => n == -1 ? CanBack : n >= 0 && n < _whoEnabled.Length && _whoEnabled[n],
            _ => throw new InvalidOperationException($"The menu is waiting for {Prompt}, not a choice."),
        };
        if (!ok)
            throw new ArgumentOutOfRangeException(nameof(n), $"No choice {n}.");
        _choice = n;
        Resume();
    }

    public void Continue()
    {
        if (Prompt != P4MenuPrompt.Wait)
            throw new InvalidOperationException($"The menu is waiting for {Prompt}, not SPACE.");
        Resume();
    }

    public void Answer(bool yes)
    {
        if (Prompt != P4MenuPrompt.Confirm)
            throw new InvalidOperationException($"The menu is waiting for {Prompt}, not yes / no.");
        _yes = yes;
        Resume();
    }

    private P4MenuPrompt Pick(IEnumerable<P4Choice> choices, bool canBack = true, string? menu = null,
        P4ListWindow? list = null)
    {
        _choices.Clear();
        _choices.AddRange(choices);
        CanBack = canBack;
        IconMenu = menu;
        ListWindow = list;
        return P4MenuPrompt.Pick;
    }

    private P4MenuPrompt Who(Func<P4Member, bool>? enabled = null, string? menu = null)
    {
        _whoEnabled = [.. Party.Members.Select(m => enabled?.Invoke(m) ?? true)];
        CanBack = true;
        IconMenu = menu;
        return P4MenuPrompt.Who;
    }

    /// <summary><paramref name="prompt"/> with the original's panel <paramref name="menu"/> (<see cref="IconMenu"/>).</summary>
    private P4MenuPrompt Icons(string menu, P4MenuPrompt prompt)
    {
        IconMenu = menu;
        return prompt;
    }

    private void Say(string id, params object[] args) => _window.Print(P4MessageWindow.Format(id, args));

    // --- F Profile (_inspect, _detail, _page2) --------------------------------------------------------------------------

    private IEnumerable<P4MenuPrompt> ProfileMenu()
    {
        Title = GameText.S("p4x.0x40540");
        while (true)
        {
            Profile = null;
            _window.Clear();
            Say("p4x.0x4054a");
            yield return Who(menu: "_inspect");
            if (_choice < 0)
                yield break;
            Marked = _choice;
            var m = Party.Members[_choice];
            // _detail (s140, static [C]): each page waits on its own SPACE / N buttons (window 36, IconMenu). SPACE leaves the
            // sheet on every page; N goes 1 → 2 (_detail), 2 → 3 (_page2) and back 3 → 2 (_page3's "N:前頁へ").
            var page = 1;
            while (true)
            {
                Profile = (m, page);
                if (page == 2)
                {
                    // _page2: A–E, each press moves the mark one down (wrapping); N (the sixth line) turns to page 3.
                    yield return Pick(Enumerable.Range(0, 5).Select(k =>
                            new P4Choice($"{(char)('A' + k)} {StrategyText(k, m.Strategy[k] & 0x0F)}"))
                        .Append(new P4Choice(GameText.S("p4x.0x4063c"))), menu: "_p_info3menu");
                    if (_choice is >= 0 and < 5)
                    {
                        // _page2 writes (low nibble + 1) wrapped, which also clears D's "covering" bits.
                        var k = _choice;
                        m.Strategy[k] = ((m.Strategy[k] & 0x0F) + 1) % S.MemberStrategyChoices[k];
                        if (k == 3 && m.Strategy[3] == CoverCompanions)
                        {
                            // The code asks "whom?" in the first fight (_protwho); the remaster asks here (SetSupport).
                            var self = Party.Members.IndexOf(m);
                            yield return Who(x => x != m, menu: ""); // the remaster's own question: no panel
                            if (_choice >= 0 && _choice != self)
                                P4Battle.SetSupport(m, _choice);
                        }
                        continue;
                    }
                    if (_choice == 5)
                    {
                        page = 3;
                        continue;
                    }
                    break;
                }
                yield return Pick([new(GameText.S(page == 1 ? "p4x.0x40a2a" : "p4x.0x42a3e"))],
                    menu: page == 1 ? "_detailmenu" : "_page3");
                if (_choice < 0)
                    break;
                page = 2;
            }
        }
    }

    /// <summary>Movement strategy D value 2: "Support companions" (the member covered is set with <see cref="P4Battle.SetSupport"/>).</summary>
    public const int CoverCompanions = 2;

    /// <summary>The text of strategy <paramref name="letter"/> (0–4 = A–E) value <paramref name="value"/> (<c>_ds1</c> lists 2–6).</summary>
    public static string StrategyText(int letter, int value)
    {
        int[] first = [0x4085c, 0x4087d, 0x4087d, 0x408aa, 0x40907];
        int[][] all =
        [
            [0x4085c, 0x40867, 0x40872], [0x4087d, 0x40882], [0x4087d, 0x40882, 0x4088d, 0x40896, 0x408a1],
            [0x408aa, 0x408b7, 0x408c0, 0x408cf, 0x408e0, 0x408ed, 0x408fc],
            [0x40907, 0x40912, 0x40925, 0x4092e, 0x40949, 0x4095c],
        ];
        _ = first;
        var list = all[letter];
        return GameText.S("p4x.0x" + list[Math.Clamp(value, 0, list.Length - 1)].ToString("x"));
    }

    /// <summary>The party strategy texts (A attitude, B flight; <c>_ds1</c> lists 0–1).</summary>
    public static string PartyStrategyText(int letter, int value)
    {
        int[][] all = [[0x407e8, 0x407ef, 0x407f6, 0x407ff], [0x40810, 0x40823, 0x4083a, 0x4084f]];
        var list = all[letter];
        return GameText.S("p4x.0x" + list[Math.Clamp(value, 0, list.Length - 1)].ToString("x"));
    }

    // --- P Party info (_party_info, _p_item) ----------------------------------------------------------------------------

    /// <summary>The pack's items by kind (<c>_p_item</c>): shields, armour, weapons, missiles, potions, treasures.</summary>
    public IReadOnlyList<int> ItemsFound()
    {
        var first = S.PartyInfoKindFirstIds;
        var counts = new int[first.Count];
        foreach (var id in Party.Pack)
        {
            if (id == 0)
                continue;
            for (var k = first.Count - 1; k >= 0; k--)
            {
                if (id >= first[k])
                {
                    counts[k]++;
                    break;
                }
            }
        }
        return counts;
    }

    private IEnumerable<P4MenuPrompt> PartyInfo()
    {
        Title = GameText.S("p4x.0x43374");
        while (true)
        {
            _window.Clear();
            // D4-062: the manual's gold gained since town and full experience (the town gate's pool × 10), or the code's
            // whole purse and the pool as stored (÷ 10) [C].
            var code = _o.P4Code(P4RuleOptions.PartyInfo);
            _window.Line(GameText.S("p4x.0x43381") + " " + (code ? Party.Gold : Party.GoldSinceTown));
            _window.Line(GameText.S("p4x.0x4338a") + " " + (code ? Party.ExperiencePool : Party.ExperiencePool * 10));
            _window.Line(GameText.S("p4x.0x4339b"));
            var found = ItemsFound();
            for (var k = 0; k < found.Count; k++)
                _window.Line("  " + GameText.S("p4x.0x" + new[] { 0x43332, 0x4333b, 0x43346, 0x43351, 0x43360, 0x43369 }[k]
                    .ToString("x")) + " " + found[k]);
            _window.Line(GameText.S("p4x.0x433ac"));
            yield return Pick([
                new($"A {PartyStrategyText(0, Party.StoryFlags[P4Party.AttitudeFlag])}"),
                new($"B {PartyStrategyText(1, Party.StoryFlags[P4Party.FleeFlag])}"),
            ], menu: "_p_info1menu"); // _party_info: SPACE (window 36's one button) leaves, A / B turn the strategy
            if (_choice < 0)
                yield break;
            var flag = _choice == 0 ? P4Party.AttitudeFlag : P4Party.FleeFlag;
            Party.StoryFlags[flag] = (byte)((Party.StoryFlags[flag] + 1) % S.PartyStrategyChoices[_choice]);
        }
    }

    // --- C Spells (_ocast, _getmagic, _pmagic3) -------------------------------------------------------------------------

    /// <summary>The spells <paramref name="caster"/> may cast at <paramref name="place"/> now (known, MP ≥ cost; <c>_splist</c>).</summary>
    public IReadOnlyList<int> CastableSpells(P4Member caster, P4CastPlace place) =>
        [.. P4TownRules.CastList(_o, S, place)
            .Where(n => caster.Knows(n) && P4Combat.MpCost(_o, _t.Combat, n) <= caster.Mp)];

    private IEnumerable<P4MenuPrompt> Cast(P4CastPlace place, int map, int encounterLevel)
    {
        Title = GameText.S("p4x.0x432ae");
        var cast = false;
        while (Transfer is null)
        {
            // s156: after a cast the box keeps the effect's lines above the next caster pick (no wait read them).
            if (!cast)
                _window.Clear();
            cast = false;
            Say("p4x.0x432b9");
            yield return Who(menu: "_ocast");
            if (_choice < 0)
                yield break;
            var ci = _choice;
            Marked = ci;
            var caster = Party.Members[ci];
            var spells = CastableSpells(caster, place);
            // _getmagic opens its "Spell List" in window 34, the panel's place (s140 [C]); s151: the Client draws it there
            // (_splist: every spell of the place lettered A…, the name and button only where castable; SPACE:Quit above).
            var page = P4TownRules.CastList(_o, S, place);
            var offered = spells.ToList();
            yield return Pick(spells.Select(n => new P4Choice(P4Names.At(_data.Names.Spells, n - 1))), menu: "",
                list: new P4ListWindow("_getmagic", [.. page.Select(n => offered.IndexOf(n) is >= 0 and var i
                    ? new P4ListRow(P4Names.At(_data.Names.Spells, n - 1), i) : new P4ListRow(null, null))], [-1]));
            if (_choice < 0)
                continue;
            var spell = spells[_choice];
            var target = 0;
            if (S.TargetedSpells.Contains(spell))
            {
                Say("p4x.0x432d1");
                yield return Who(menu: "_ocast.2");
                if (_choice < 0)
                    yield break;
                target = _choice;
            }
            _window.Clear();
            Say("p4x.0x432d9", caster.Name);
            foreach (var p in Effect(caster, spell, Party.Members[target], place, map, encounterLevel))
                yield return p;
            // _ocast: the MP cost is taken after the effect, whatever happened (floor 0). s156: then straight back to the
            // caster pick (0x153C8 → 0x15458): the only waits are _pmagic3's own (Effect), so a healing spell's line
            // stays in the box under the next pick.
            caster.Mp = Math.Max(0, caster.Mp - P4Combat.MpCost(_o, _t.Combat, spell));
            cast = true;
        }
    }

    private IEnumerable<P4MenuPrompt> Effect(P4Member caster, int spell, P4Member target, P4CastPlace place, int map,
        int encounterLevel)
    {
        Party.Sounds.Note(P4Sounds.SpellCast); // _pmagic3 0x2D892: _sdmag on entry
        switch (spell)
        {
            case >= 1 and <= 4:
                var hp = P4Combat.HealingSpell(_o, _t.Combat, _rng, spell);
                target.Hp = Math.Min(target.HpMax, target.Hp + Math.Max(0, hp));
                P4Body.Heal(target.Wounds, spell);
                Say("p4x.0x453f0", target.Name);
                break;
            case 13:
                Party.Levitating = true;
                Party.LevitationSteps = P4Outdoor.LevitationSteps(_o, _t.Outdoor,
                    Party.Members.Select(m => P4Creation.Stride(_o, _t.Creation, m.Race, m.Class)));
                Say("p4x.0x4540d");
                break;
            case 15:
                foreach (var p in Identify(caster))
                    yield return p;
                break;
            case 40:
                var chance = target.Condition != P4Member.Dead ? 0 : 50 + 5 * (caster.Level - target.Level);
                if (P4Dice.Rand(_rng, 100) < chance)
                {
                    Array.Clear(target.Wounds);
                    target.Condition = P4Member.Okay;
                    Say("p4x.0x45424", target.Name);
                }
                else
                    Say("p4x.0x4543c");
                yield return P4MenuPrompt.Wait; // 0x2D55E: _spc_wait after either line
                break;
            case 47 or 65:
                foreach (var p in Transport(caster, spell == 65))
                    yield return p;
                break;
            case 49:
                var level = place == P4CastPlace.Dungeon
                    ? (map >= 1 && map <= S.DungeonMonsterLevels.Count ? S.DungeonMonsterLevels[map - 1] : 0)
                    : Math.Clamp(encounterLevel, P4Outdoor.MinReportedLevel, P4Outdoor.MaxReportedLevel);
                level = P4Outdoor.MonsterEvaluation(_o, _t.Amounts, _rng, level, caster.Level);
                Say("p4x.0x4547f");
                Say("p4x.0x45498", level);
                yield return P4MenuPrompt.Wait; // 0x2D7B4
                break;
            case 50:
                foreach (var p in MagicBag())
                    yield return p;
                break;
            case 63:
                Say(new[] { "p4x.0x454a8", "p4x.0x454b4", "p4x.0x454bc", "p4x.0x454c4" }[P4Dice.Rand(_rng, 4) - 1]);
                Say("p4x.0x454cc");
                yield return P4MenuPrompt.Wait; // 0x2D83E
                Say("p4x.0x454e1", GameText.S(S.DivineAidHints[Math.Clamp(map, 0, S.DivineAidHints.Count - 1)]));
                yield return P4MenuPrompt.Wait; // 0x2D86A
                break;
            // s156: healing (1–4), Levitation (13) and a failed Transportation print their line with no _spc_wait.
        }
    }

    /// <summary>
    /// Transportation / Super Transport (<c>_cast_47</c>): the caster's current town picks the group (1–12 → 0, 14 → 1,
    /// 17 / 18 → 2, 21 → 3); the player picks a town by letter and confirms (Super Transport: a random one); else
    /// "Transport! …Nothing changed."
    /// </summary>
    private IEnumerable<P4MenuPrompt> Transport(P4Member caster, bool super)
    {
        if (!S.TownTransportGroup.TryGetValue(caster.Town, out var g))
        {
            Say("p4x.0x4545d");
            yield break;
        }
        var towns = S.TransportGroups[g];
        int town;
        if (super)
            town = towns[P4Dice.Rand(_rng, towns.Count) - 1];
        else
        {
            Say("p4x.0x452fe");
            // _cast_47 (s140 [C]): "Transport" panel (SPACE:Quit), the towns as lines A… in that window under the icon
            // (y 60 + 16 · i); s151: the Client draws them there (ListWindow), the text column keeps its lines.
            yield return Pick(towns.Select((n, i) => new P4Choice($"{(char)('A' + i)} {TownName(n)}")), menu: "_cast_47",
                list: P4ListWindow.Of("_cast_47", towns.Select(TownName)));
            if (_choice < 0)
                yield break;
            town = towns[_choice];
            Say("p4x.0x4530a", TownName(town));
            yield return P4MenuPrompt.Confirm;
            if (!_yes)
                yield break;
        }
        Say("p4x.0x45452");
        yield return P4MenuPrompt.Wait; // _pmagic3 0x2D6F0
        Transfer = new P4Transfer(P4Transfer.Town, town, 0, 0, 0);
    }

    private string TownName(int town) => GameText.S(S.TownNameIds[town]);

    private IEnumerable<P4MenuPrompt> MagicBag()
    {
        Say("p4x.0x4532b");
        Say("p4x.0x4534a");
        yield return P4MenuPrompt.Wait;
        _window.Clear();
        _window.Line(GameText.S("p4x.0x45354"));
        yield return P4MenuPrompt.Wait;
        _window.Line(GameText.S("p4x.0x45389"));
        yield return P4MenuPrompt.Confirm;
        var slot = _yes ? Array.IndexOf(Party.Pack, 0) : -1;
        if (!Party.CanAfford(_o, S.MagicBagCost))
            slot = -1;
        if (slot < 0)
        {
            Say("p4x.0x4539e");
            yield return P4MenuPrompt.Wait; // _cast_50 0x2D3E2
            yield break;
        }
        Party.TrySpend(_o, S.MagicBagCost, TreasuryEntryKind.Magic);
        var item = S.MagicBagItems[P4Dice.Rand(_rng, S.MagicBagItems.Count) - 1];
        Say("p4x.0x453bd");
        Say("p4x.0x453da", _data.Items.Name(item));
        Party.Pack[slot] = item;
        yield return P4MenuPrompt.Wait; // _cast_50 0x2D310
    }

    /// <summary>
    /// Identify (<c>_ident</c>, <c>_ident_mi</c>): pick a member and one of his items; its appraisal text (D4-053: under the
    /// manual the cast may fail, <see cref="P4Outdoor.IdentifySucceeds"/>).
    /// </summary>
    private IEnumerable<P4MenuPrompt> Identify(P4Member caster)
    {
        var title = Title;
        Title = GameText.S("p4x.0x45756");
        var ok = P4Outdoor.IdentifySucceeds(_o, _t.Amounts, _rng, caster.Level);
        while (true)
        {
            // _ident (s140 [C]): _setident's " SPACE" panel over the member buttons and the item list.
            yield return Who(m => m.Items.Any(i => i != 0), menu: "_setident");
            if (_choice < 0)
                break;
            var m = Party.Members[_choice];
            yield return Pick(m.Items.Select(i => new P4Choice(i == 0 ? "-" : _data.Items.Name(i), i != 0, i)), menu: "_setident");
            if (_choice < 0)
                continue;
            _window.Clear();
            if (!ok)
                Say("p4x.0x4543c");
            else
            {
                Party.Sounds.Note(P4Sounds.Identify); // _ident_mi 0x2DBBC: _sdmag before its window opens
                foreach (var id in S.IdentifyLines[m.Items[_choice]])
                    Say(id);
            }
            yield return P4MenuPrompt.Wait;
            break; // _ident (s140 [C]): one item is appraised (_ident_mi, _spc_wait), then the spell ends
        }
        Title = title;
    }

    // --- U Use item (_use, _use_hp, _use_mp, _use_op, _use_mi, _use_sc, _use_box) -------------------------------------

    private IEnumerable<P4MenuPrompt> Use()
    {
        Title = GameText.S("p4x.0x4270a");
        var who = 0;
        while (Transfer is null)
        {
            Marked = who;
            var m = Party.Members[who];
            var holding = HeldItem != 0;
            var choices = m.Items.Select(i => new P4Choice(i == 0 ? "-" : _data.Items.Name(i), !holding && i != 0, i)).ToList();
            choices.AddRange(Party.Members.Select(x => new P4Choice(x.Name, holding || x.Items.Any(i => i != 0))));
            choices.Add(new(GameText.S("p4x.0x42718"), !holding));
            yield return Pick(choices, canBack: false, menu: "_setuse"); // _setuse: "L:Done" (the last line)
            var c = _choice;
            _window.Clear();
            if (c < P4Member.CarriedSlots)
            {
                var item = m.Items[c];
                if (item is >= 0x55 and <= 0x80 or 0x87)
                {
                    // Potions and the Rod leave the slot at once; then the drinker is picked.
                    (m.Items[c], m.ItemCharges[c]) = (0, 0);
                    HeldItem = item;
                }
                else if (item is >= 0x81 and <= 0x8B)
                    UseMagicItem(m, c);
                else if (item is >= 0x8C and <= 0x90)
                {
                    foreach (var p in Scroll(item))
                        yield return p;
                }
                else if (item == 0x91)
                {
                    (m.Items[c], m.ItemCharges[c]) = (0, 0);
                    foreach (var id in S.BoxLines)
                        _window.Line(GameText.S(id));
                    yield return P4MenuPrompt.Wait;
                    if (Party.Ship == P4ShipState.Aboard)
                        Party.Ship = P4ShipState.Moored;
                    Transfer = S.BoxTarget;
                }
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
                foreach (var p in Drink(Party.Members[c], HeldItem))
                    yield return p;
                HeldItem = 0;
                continue;
            }
            break;
        }
    }

    /// <summary><c>_spoile</c> then <c>_use_hp</c> / <c>_use_mp</c> / <c>_use_op</c> (items.md §2–§4).</summary>
    private IEnumerable<P4MenuPrompt> Drink(P4Member d, int item)
    {
        if (item != 0x87 && P4Body.PotionSpoiled(_rng))
        {
            _window.Line(GameText.S("p4x.0x425c4"));
            Damage(d, P4Dice.Rand(_rng, 8));
            // _spoile prints the line and goes on without _spc_wait (s140 [C]): s156, as the original, straight back to
            // _setuse's item pick, the line still in the box.
            yield break;
        }
        Party.Sounds.Note(P4Sounds.ItemUsed); // _use_hp 0xEEAA / _use_mp 0xED9A / _use_op 0xF878: _sdmag
        if (item is >= 0x55 and <= 0x5E)
        {
            var (hp, heal) = P4Body.HealingPotion(_o, _t.Towns, item - 0x54);
            d.Hp = Math.Min(d.HpMax, d.Hp + hp);
            P4Body.Heal(d.Wounds, heal);
            yield break;
        }
        if (item is >= 0x5F and <= 0x68)
        {
            d.Mp = Math.Min(d.MpMax, d.Mp + 3 * (item - 0x5E));
            yield break;
        }
        int Up(int v, int below, int by = 1) => v < below ? v + by : v;
        var a = d.Abilities; // Str, Int, Dex, Con, Cha, Luck
        switch (item)
        {
            case 0x69: a[0] = Up(a[0], 24); break;
            case 0x6A: a[2] = Up(a[2], 24); break;
            case 0x6B: a[4] = Up(a[4], 24); break;
            case 0x6C: a[1] = Up(a[1], 24); break;
            case 0x6D: a[0] = Up(a[0], 30, 2); d.Mp = d.MpMax; break; // +2F as the code reads (items.md §4)
            case 0x6E: d.Spells[1] = 1; break;
            case 0x6F: d.Spells[6] = 1; break;
            case 0x70: d.Spells[37] = 1; break;
            case 0x71: d.Spells[55] = 1; break;
            case 0x72: d.Spells[41] = 1; break;
            case 0x73 when d.Race != P4Ids.Duck: d.SavedRace = d.Race; d.Race = P4Ids.Duck; break;
            case 0x74 when d.Race == P4Ids.Duck: d.Race = d.SavedRace; break;
            case 0x75: a[0] = 22; break;
            case 0x76: a[0] = 19; break;
            case 0x77: a[0] = 9; break;
            case 0x78: a[1] = 18; break;
            case 0x79: d.Hp = (d.HpMax + 10) & 0xFF; d.Mp = (d.MpMax + 10) & 0xFF; break;
            case 0x7A: a[0] = a[0] > 4 ? a[0] - 1 : a[0]; a[1] = Up(a[1], 24); a[4] = Up(a[4], 24, 2); break;
            case 0x7B: a[2] = Up(a[2], 24); d.MoveBase = Math.Max(10, Up(d.MoveBase, 18, 2)); break;
            case 0x7C: Damage(d, P4Dice.Rand(_rng, 26) + 24); break;
            case 0x7D:
                for (var i = 0; i < 6; i++)
                {
                    a[i] = a[i] < 24 ? a[i] + P4Dice.Rand(_rng, 2) - 1 : a[i];
                    a[i] = a[i] > 3 ? a[i] - (P4Dice.Rand(_rng, 2) - 1) : a[i];
                }
                break;
            case 0x7E:
                d.Spells[64] = 1;
                foreach (var id in new[] { "p4x.0x425f0", "p4x.0x42613", "p4x.0x42636", "p4x.0x42659" })
                    _window.Line(GameText.S(id));
                yield return P4MenuPrompt.Wait;
                break;
            case 0x7F: d.MoveBase = Up(d.MoveBase, 18, 2); break;
            case 0x80: if (d.Hp + 100 < 256) d.Hp += 100; break;
            case 0x87: Array.Clear(d.Wounds); d.Condition = P4Member.Okay; break;
        }
    }

    /// <summary><c>_ddamage</c> outside combat: HP − d; d ≥ HP → HP 0 and Dead.</summary>
    private void Damage(P4Member m, int d)
    {
        Party.Sounds.Note(P4Sounds.MemberDamaged); // _ddamage 0x1A348: _sddam
        if (d >= m.Hp)
        {
            m.Hp = 0;
            m.Condition = P4Member.Dead;
        }
        else
            m.Hp -= d;
    }

    /// <summary><c>_use_mi</c> (items.md §5) on the owner, with the slot's charges.</summary>
    private void UseMagicItem(P4Member m, int slot)
    {
        Party.Sounds.Note(P4Sounds.ItemUsed); // _use_mi 0xFA82: _sdmag on entry, charges or not
        var charges = m.ItemCharges[slot];
        switch (m.Items[slot])
        {
            case 0x81:
                while (charges > 0 && m.Mp < m.MpMax)
                {
                    charges--;
                    m.Mp++;
                }
                if (m.Abilities[0] < 24)
                    m.Abilities[0]++;
                break;
            case 0x83 when charges >= 2:
                charges -= 2;
                m.Hp = Math.Min(m.HpMax, m.Hp + P4Dice.Rand(_rng, 27));
                break;
            case 0x89 when m.Class == P4Ids.Fighter && charges > 0:
                charges = 0;
                if (m.Abilities[0] < 30)
                    m.Abilities[0] += 2;
                break;
        }
        m.ItemCharges[slot] = charges;
    }

    /// <summary><c>_use_sc</c>: the scroll file scr(item − 0x8C + 1), 19 lines a page until the '*' line.</summary>
    private IEnumerable<P4MenuPrompt> Scroll(int item)
    {
        var n = item - 0x8C + 1;
        var lines = new List<string>();
        for (var k = 0; k < 200; k++)
        {
            var id = $"scr{n}.{k}";
            var s = GameText.S(id);
            if (s == id)
                continue;
            lines.Add(s);
        }
        ReadingScroll = true;
        for (var i = 0; i < lines.Count; i += 10)
        {
            _window.Clear();
            foreach (var l in lines.Skip(i).Take(10))
                _window.Line(l);
            yield return P4MenuPrompt.Wait;
        }
        ReadingScroll = false;
    }
}
