using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The town buildings (Guild, Armory, Bank, Hall, item distribution / exchange, the ending) and the party menus F / P / C / U
/// of every place (s102; <see cref="P4TownLoop"/> screens, <see cref="P4Menus"/>). Keys: digits 1–9 or Up / Down + Enter
/// pick a line or a member, Space goes back where the screen allows it, Y / N answer, typed digits / letters fill amounts
/// and names. The town menu also takes T (trade), D (share out items), F, C, U; outdoors F, P, C, U; in a dungeon F, C, U.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private P4Menus? M => Game.Menus;

    /// <summary>True while a party menu is open (its keys and lines take over the screen).</summary>
    public bool MenuOpen => M is { Prompt: not P4MenuPrompt.Closed };

    /// <summary>F / P / C / U open a party menu (P outdoors and in dungeons, <c>_omain</c> / <c>_dmain</c>; not in town); true when the key was one.</summary>
    private bool CommandKey(Keys key)
    {
        if (key == Keys.S)
        {
            OpenSystem(); // the S icon: System (_syscon)
            return true;
        }
        if (M is null)
            return false;
        P4MenuCommand? c = key switch
        {
            Keys.F => P4MenuCommand.Profile,
            Keys.P when Game.Place != P4Place.Town => P4MenuCommand.PartyInfo,
            Keys.C => P4MenuCommand.Cast,
            Keys.U => P4MenuCommand.Use,
            _ => null,
        };
        if (c is null)
            return false;
        Cursor = 0;
        Game.OpenMenu(c.Value);
        return true;
    }

    private void MenusKey(Keys key)
    {
        var m = M!;
        switch (m.Prompt)
        {
            case P4MenuPrompt.Pick:
                ChoiceKey(key, m.Choices, m.CanBack, m.Choose);
                break;
            case P4MenuPrompt.Who:
                WhoKey(key, m.WhoEnabled, m.CanBack, m.Choose);
                break;
            case P4MenuPrompt.Wait when key is Keys.Space or Keys.Enter:
                m.Continue();
                break;
            case P4MenuPrompt.Confirm when key is Keys.Y or Keys.N:
                m.Answer(key == Keys.Y);
                break;
        }
    }

    /// <summary>A list pick: digits 1–9 (the n-th enabled line), Up / Down + Enter, Space = back where allowed.</summary>
    private void ChoiceKey(Keys key, IReadOnlyList<P4Choice> choices, bool canBack, Action<int> choose)
    {
        var count = choices.Count;
        if (key == Keys.Space && canBack)
        {
            Cursor = 0;
            choose(-1);
            return;
        }
        if (count == 0)
            return;
        int? n = key switch
        {
            >= Keys.D1 and <= Keys.D9 => key - Keys.D1,
            >= Keys.NumPad1 and <= Keys.NumPad9 => key - Keys.NumPad1,
            Keys.Enter => Cursor % count,
            _ => null,
        };
        if (key == Keys.Up)
            Cursor = (Cursor + count - 1) % count;
        else if (key == Keys.Down)
            Cursor = (Cursor + 1) % count;
        else if (n is int i && i < count && choices[i].Enabled)
        {
            Cursor = 0;
            choose(i);
        }
    }

    private void WhoKey(Keys key, IReadOnlyList<bool> enabled, bool canBack, Action<int> choose) =>
        ChoiceKey(key, [.. Game.Party.Members.Select((m, i) => new P4Choice(m.Name, i < enabled.Count && enabled[i]))],
            canBack, choose);

    /// <summary>The town's building prompts (Pick, Who, Number, Text); true when the key belonged to one.</summary>
    private bool TownScreenKey(Keys key)
    {
        switch (W.Prompt)
        {
            case P4TownPrompt.Pick:
                ChoiceKey(key, W.Choices, W.CanBack, W.Choose);
                return true;
            case P4TownPrompt.Who:
                WhoKey(key, W.WhoEnabled, W.CanBack, W.Choose);
                return true;
            case P4TownPrompt.Number or P4TownPrompt.Text:
                if (key == Keys.Back && _typed.Length > 0)
                    _typed = _typed[..^1];
                else if (key == Keys.Enter)
                {
                    var text = _typed;
                    _typed = "";
                    if (W.Prompt == P4TownPrompt.Number)
                        W.EnterNumber(long.TryParse(text, out var n) ? n : 0);
                    else
                        W.EnterText(text);
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>The town menu's own letters: T trade, D share out items, and the party menus.</summary>
    private bool TownCommandKey(Keys key)
    {
        switch (key)
        {
            case Keys.T:
                W.Command(P4TownCommand.Trade);
                return true;
            case Keys.D:
                W.Command(P4TownCommand.Distribute);
                return true;
            default:
                return CommandKey(key);
        }
    }

    /// <summary>A typed character for the town's amount / name prompts; true when one took it.</summary>
    private bool TownTextInput(char c)
    {
        if (Game.Place != P4Place.Town)
            return false;
        if (W.Prompt == P4TownPrompt.Text && _typed.Length < W.TextLimit && c >= ' ' && c < 0x7f)
            _typed += c;
        else if (W.Prompt == P4TownPrompt.Number && _typed.Length < 5 && char.IsAsciiDigit(c))
            _typed += c;
        else
            return false;
        return true;
    }

    // --- Lines --------------------------------------------------------------------------------------------------------------

    private List<MenuLine> MenuLines()
    {
        var m = M!;
        var lines = new List<MenuLine>();
        if (m.Title is { } t)
            lines.Add(new(t, MenuLineStyle.Title));
        if (m.Profile is { } p)
            lines.AddRange(ProfileLines(p.Member, p.Page));
        foreach (var l in m.Box)
            if (l.Length > 0)
                lines.Add(new(l, MenuLineStyle.Normal));
        AddPrompt(lines, m.Prompt switch
        {
            P4MenuPrompt.Pick => P4TownPrompt.Pick,
            P4MenuPrompt.Who => P4TownPrompt.Who,
            P4MenuPrompt.Confirm => P4TownPrompt.Confirm,
            _ => P4TownPrompt.Wait,
        }, m.Choices, m.WhoEnabled);
        return lines;
    }

    private void AddPrompt(List<MenuLine> lines, P4TownPrompt prompt, IReadOnlyList<P4Choice> choices, IReadOnlyList<bool> who)
    {
        switch (prompt)
        {
            case P4TownPrompt.Pick:
                for (var i = 0; i < choices.Count; i++)
                    lines.Add((choices[i].Enabled ? Option(i, $"{i + 1} {choices[i].Text}")
                        : new($" {i + 1} {choices[i].Text}", MenuLineStyle.Dim)) with { Icon = ItemIconName(choices[i].Item) });
                break;
            case P4TownPrompt.Who:
                lines.Add(new(UiText.S("p4.dungeon.who"), MenuLineStyle.Header));
                for (var i = 0; i < Game.Party.Members.Count; i++)
                {
                    var on = i < who.Count && who[i];
                    lines.Add(on ? Option(i, $"{i + 1} {Game.Party.Members[i].Name}")
                        : new($" {i + 1} {Game.Party.Members[i].Name}", MenuLineStyle.Dim));
                }
                break;
            case P4TownPrompt.Confirm:
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4TownPrompt.Number:
                lines.Add(new(UiText.F("p4.dungeon.number_x", _typed), MenuLineStyle.Selected));
                break;
            case P4TownPrompt.Text:
                lines.Add(new(UiText.F("p4.dungeon.letters_x", _typed), MenuLineStyle.Selected));
                break;
        }
    }

    /// <summary>A building's lines: its title, the marked member, the shop / roster / Hall sheet, the box and the prompt.</summary>
    private List<MenuLine> TownScreenLines()
    {
        var lines = new List<MenuLine>();
        if (W.ScreenTitle is { } t)
            lines.Add(new(t, MenuLineStyle.Title));
        lines.Add(new(UiText.F("p4.town.gold_x", Game.Party.Gold), MenuLineStyle.Dim));
        if (W.Marked is int mk && mk < Game.Party.Members.Count)
            lines.Add(new("> " + Game.Party.Members[mk].Name, MenuLineStyle.Header));
        if (W.HeldItem != 0 && Game.Services is { } s)
        {
            // Share out (_dvitmlst, FORMAT.md 6.9 "Item lists"): the item being placed and the next four of the sorted
            // queue, each with its icon; Trade's held item has no row of its own in the original (no icon).
            var sharing = W.IconMenu == "_distribute";
            lines.Add(new(UiText.F("p4.town.held_x", s.Items.Name(W.HeldItem)), MenuLineStyle.Header,
                sharing ? ItemIconName(W.HeldItem) : null, Lit: sharing)); // s158: row 0 lit (_wxbox XOR 15)
            if (sharing)
                foreach (var q in W.Queue.Take(DistributeRows - 1))
                    lines.Add(new("  " + s.Items.Name(q), MenuLineStyle.Dim, ItemIconName(q)));
        }
        if (W.FirstAncestorSheet is { } a1)
            lines.AddRange(SheetLines(GameText.S("p4x.0x42a0b"), a1));
        if (W.SecondAncestorSheet is { } a2)
            lines.AddRange(SheetLines(GameText.S("p4x.0x42a14"), a2));
        if (W.CreationSheet is { } cs && W.Profile is null)
            lines.AddRange(SheetLines(W.FirstAncestorSheet is null ? null : GameText.S("p4x.0x42d26"), cs));
        if (W.Profile is { } p)
            lines.AddRange(ProfileLines(p, 1));
        if (W.HallPerson is { } h)
            lines.AddRange(HallLines(h));
        foreach (var l in W.Box)
            if (l.Length > 0)
                lines.Add(new(l, MenuLineStyle.Normal));
        if (W.Prompt == P4TownPrompt.Shares)
        {
            lines.Add(new(UiText.F("p4.town.share_gold_x", W.ShareAmount), MenuLineStyle.Header));
            var n = Game.Party.Members.Count;
            for (var i = 0; i < n; i++)
                lines.Add(Option(i, $"{Game.Party.Members[i].Name,-10} {(_shares.Length == n ? _shares[i] : 1)}"));
        }
        else
            AddPrompt(lines, W.Prompt, W.Choices, W.WhoEnabled);
        return lines;
    }

    /// <summary>
    /// A creation window (<c>_dsp_buf</c>): Name ("?" until named), Race, Class, then the five abilities and the move base,
    /// which the original prints under "Luck" (<c>ancestry.md</c> §5.4). A race-only sheet (ancestor 2 before its class) shows
    /// the race line alone.
    /// </summary>
    private IEnumerable<MenuLine> SheetLines(string? title, P4CreationSheet s)
    {
        var names = Game.Services!.Names;
        if (title is not null)
            yield return new(title, MenuLineStyle.Header);
        yield return new($"{GameText.S("p4x.0x401f7")}: {(s.Name.Length == 0 ? "?" : s.Name)}", MenuLineStyle.Normal);
        yield return new($"{GameText.S("p4x.0x401ed")}: {P4Names.At(names.Races, s.Race)}", MenuLineStyle.Normal);
        if (s.Class < 0)
            yield break;
        yield return new($"{GameText.S("p4x.0x40201")}: {P4Names.At(names.Classes, s.Class)}", MenuLineStyle.Normal);
        yield return new(UiText.F("p4.create.abilities_x_y", string.Join(" ", s.Abilities), s.MoveBase), MenuLineStyle.Normal);
    }

    private IEnumerable<MenuLine> HallLines(P4HallPerson h)
    {
        var names = Game.Services!.Names;
        yield return new(h.Name, MenuLineStyle.Header);
        yield return new($"{P4Names.At(names.Races, h.Race)} {P4Names.At(names.Classes, h.Class)} L{h.Level}", MenuLineStyle.Normal);
        // _dsp_hinfo: slots 0-2 name their origin from _origin, imports from the list at 0x4187a (III's origins).
        var origin = h.Slot < P4Hall.FirstImportSlot ? P4Names.At(names.Origins, h.Origin)
            : h.Origin is >= 0 and < 6 ? GameText.S(ImportOrigins[h.Origin]) : $"#{h.Origin}";
        yield return new($"{P4Names.At(names.Socials, h.Social)}, {origin}", MenuLineStyle.Normal);
        yield return new(string.Join(" ", h.Stats), MenuLineStyle.Normal);
        yield return new(UiText.F("p4.hall.descendants_x", W.HallDescendants(h)), MenuLineStyle.Normal);
        var (a, b) = P4TownLoop.HallDeeds(h);
        yield return new(GameText.S(a) + (b.Length > 0 ? " " + GameText.S(b) : ""), MenuLineStyle.Normal);
        // Imports show their three game results: Completed / Not completed / blank (not played; 0x41bc7 is empty).
        if (h.Slot >= P4Hall.FirstImportSlot)
            for (var g = 0; g < 3 && g < h.Results.Count; g++)
                yield return new(UiText.F("p4.hall.result_x_y", new[] { "I", "II", "III" }[g],
                    h.Results[g] >= 100 ? GameText.S("p4x.0x41ba5") : h.Results[g] > 0 ? GameText.S("p4x.0x41bb5") : ""),
                    MenuLineStyle.Normal);
        // Lord Wood's status is recomputed on display: alive unless an import is of class 4.
        yield return new(GameText.S(P4Hall.IsAlive(h, W.HallPersons) ? "p4x.0x41ba0" : "p4x.0x41b9b"), MenuLineStyle.Dim);
    }

    /// <summary>The import origins (<c>0x4187a</c>): Gelnor, Ferronrah, Netherworld, Scandor, Plane of Light, Plane of Darkness.</summary>
    private static readonly string[] ImportOrigins =
        ["p4x.0x41b69", "p4x.0x41b70", "p4x.0x41b76", "p4x.0x41b7f", "p4x.0x41b87", "p4x.0x41b91"];

    /// <summary>
    /// One ancestor line's text after "Ancestor :" (<c>_page1</c> → <c>_dsp_uni</c>, record +22…+25): a Hall person as
    /// "name a Social  Race  Class  Level n" (from the Hall record by slot), a new person as "Social  Race  Class" (the
    /// stored <c>_anc</c> snapshot: no name, no level), none as "Unknown".
    /// </summary>
    public string AncestorText(P4AncestorRef a)
    {
        ArgumentNullException.ThrowIfNull(a);
        var names = Game.Services!.Names;
        string Uni(string name, int social, int race, int cls, int level) =>
            (name.Length > 0 ? name + GameText.S("p4x.0x40a83") : "")
            + P4Names.At(names.Socials, social) + "  " + P4Names.At(names.Races, race) + "  " + P4Names.At(names.Classes, cls)
            + (level != 0 ? GameText.S("p4x.0x40a8d") + level : "");
        return a.Kind switch
        {
            P4AncestorKind.Hall when P4Hall.Persons(Game.Services.Hall, Game.Party).FirstOrDefault(h => h.Slot == a.Index) is { } h =>
                Uni(h.Name, h.Social, h.Race, h.Class, h.Level),
            P4AncestorKind.New => Uni("", a.Social, a.Race, a.Class, 0),
            _ => GameText.S("p4x.0x40ad8"),
        };
    }

    /// <summary>Profile page 1 (the sheet), 2 (combat strategy: the Pick lists it) or 3 (known spells), <c>_detail</c>.</summary>
    private IEnumerable<MenuLine> ProfileLines(P4Member m, int page)
    {
        var names = Game.Services!.Names;
        yield return new($"{m.Name}  L{m.Level} {P4Names.At(names.Races, m.Race)} {P4Names.At(names.Classes, m.Class)}",
            MenuLineStyle.Header);
        if (page == 2)
            yield break;
        if (page == 3)
        {
            for (var s = 1; s <= P4Member.SpellCount; s++)
                if (m.Knows(s))
                    yield return new(P4Names.At(names.Spells, s - 1), MenuLineStyle.Normal);
            yield break;
        }
        foreach (var a in m.Ancestors)
            yield return new(GameText.S("p4x.0x40ac9") + "  " + AncestorText(a), MenuLineStyle.Normal);
        yield return new(UiText.F("p4.profile.age_x", m.AgeUnits / 26), MenuLineStyle.Normal);
        yield return new($"{P4Names.At(names.Socials, m.Social)}, {P4Names.At(names.Origins, m.Origin)}", MenuLineStyle.Normal);
        yield return new(UiText.F("p4.profile.score_exp_gold_x_y_z", m.Score, m.Experience, m.Account), MenuLineStyle.Normal);
        yield return new(UiText.F("p4.profile.abilities_x", string.Join(" ", m.Abilities)), MenuLineStyle.Normal);
        yield return new(UiText.F("p4.profile.skills_x", string.Join(" ", m.Skills)), MenuLineStyle.Normal);
        var eq = P4Equipment.Best(Game.Options, Game.Tables.Towns, Game.Services.Items, m);
        foreach (var id in new[] { eq.Shield, eq.Armour, eq.Weapon, eq.Bow, eq.Thrown }.Where(i => i != 0))
            yield return new("* " + Game.Services.Items.Name(id), MenuLineStyle.Normal);
        foreach (var id in m.Items.Where(i => i != 0).Except([eq.Shield, eq.Armour, eq.Weapon, eq.Bow, eq.Thrown]))
            yield return new("  " + Game.Services.Items.Name(id), MenuLineStyle.Dim);
    }
}
