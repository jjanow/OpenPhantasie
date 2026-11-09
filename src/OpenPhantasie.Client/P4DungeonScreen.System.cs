using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The pages of IV's System screen (<c>_syscon</c>, <c>_acgamend</c>, <c>_acend</c>, <c>_about</c>). s158:
/// <see cref="SelectGame"/> is <c>_acgamend(0)</c> (SPACE, E, T), <see cref="SelectGameFinal"/> <c>_acgamend(1)</c> (E, T: the
/// final screen's and the wiped-out party's), <see cref="EndGame"/> <c>_acend(0)</c>, <see cref="BackToTitle"/>
/// <c>_acend(1)</c>.
/// <see cref="Save"/> is <c>_diskmenu(1)</c> (the original's three save slots, SAVE SLOTS off); <see cref="Load"/> the
/// remaster's F9 over the same window (the original loads only from the title).
/// </summary>
public enum P4SystemPage { Menu, EndGame, About, SelectGame, SelectGameFinal, BackToTitle, Save, Load }

/// <summary>
/// IV's System screen (s138; P4.X <c>_syscon</c>, FORMAT.md 6.9 "Command panel"): the S icon of the street, outdoor and
/// location-map menus opens it; its panel holds SPACE (back), D Display speed, B Sound switch, S Save game, E End game and
/// A About. The remaster runs SPACE, S (with SAVE SLOTS off <c>_disk(1)</c>'s Save page, <see cref="P4SystemPage.Save"/>;
/// on, the shell's slot picker, as F5), E and A (<c>_about</c>'s credit lines); D
/// (<c>_timelag</c>) and B (<c>_acsound</c>) have no IV setting here and only say so `[~]`. s158: E opens
/// <c>_acgamend(0)</c>'s "Select Game" (SPACE back, E End game, T Back to title), each choice asking <c>_acend</c>'s
/// Okay / Cansel (C goes back to the Select Game icons, as the original's loop). The final screen (after its save
/// question) and a wiped-out party (after <c>_deadmsg</c>'s key) open <c>_acgamend(1)</c>: E and T only. End game goes to
/// the main menu (the original's <c>_xexit</c> leaves for Human68k `[~]`), Back to title to IV's title splash with its
/// <c>_disk(0)</c> menu (the original's <c>_acend(1)</c> clears the party and starts <c>_main</c> again). It is a Client
/// screen: Core keeps no state for it, and the place's prompt waits underneath.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private P4SystemPage? _system;

    /// <summary>The Select Game page an <c>_acend</c> question goes back to on C (<c>_acgamend</c>'s mode).</summary>
    private P4SystemPage _selectGame = P4SystemPage.SelectGame;

    /// <summary>The System screen's page while it is open, else null.</summary>
    public P4SystemPage? SystemPage => _system;

    /// <summary><c>_about</c>'s lines, which the original prints into the panel's window under its SPACE icon.</summary>
    public static readonly string[] AboutLines =
        ["p4x.0x3f41c", "p4x.0x3f429", "p4x.0x3f432", "p4x.0x3f437", "p4x.0x3f440", "p4x.0x3f445", "p4x.0x3f454",
            "p4x.0x3f460", "p4x.0x3f467", "p4x.0x3f471"];

    private static string SystemPanel(P4SystemPage page) => page switch
    {
        P4SystemPage.EndGame => "_acend",
        P4SystemPage.BackToTitle => "_acend.2",
        P4SystemPage.SelectGame => "_acgamend",
        P4SystemPage.SelectGameFinal => "_acgamend.1",
        P4SystemPage.About => "_about",
        P4SystemPage.Save => SavePanelName,
        P4SystemPage.Load => LoadPanelName,
        _ => "_syscon",
    };

    /// <summary>The names of the save and load pages' panels, built from the pack's <c>"disk"</c> (<see cref="DiskPanel"/>).</summary>
    public const string SavePanelName = "_diskmenu.1", LoadPanelName = "_diskmenu.load";

    /// <summary>
    /// SAVE SLOTS off (<c>docs/plan/12-save-slots.md</c>): IV keeps its original's three slots, so Save (F5, System ▸ S, the
    /// final screen's question) opens <c>_diskmenu(1)</c> here and F9 its load page. On, the shell's slot picker runs
    /// them instead and the screen only asks for it (<see cref="P4ScreenRequest.Save"/> / <see cref="P4ScreenRequest.Load"/>).
    /// </summary>
    public bool OriginalSlots { get; set; }

    /// <summary>Whether IV's slot k (1–3) holds a save (<c>_xexfile("fames.k")</c>); the shell sets it.</summary>
    public Func<int, bool> SlotSaved { get; set; } = _ => false;

    /// <summary>The slot a <see cref="P4ScreenRequest.SaveSlot"/> / <see cref="P4ScreenRequest.LoadSlot"/> request names.</summary>
    public int RequestedSlot { get; private set; }

    /// <summary>The page the save / load page goes back to (the System menu, the final Select Game, or none).</summary>
    private P4SystemPage? _diskBack;

    /// <summary>The save or load page is up (F9's load page also over a fight or a party menu).</summary>
    private bool DiskPageUp => _system is P4SystemPage.Save or P4SystemPage.Load;

    /// <summary>The page shows the pack's panel: outside fights and party menus, where the panel strip is (else text lines).</summary>
    private bool DiskPanelShown => Game.Battle is null && !MenuOpen && DiskPanel(SystemPanel(_system!.Value)) is not null;

    /// <summary>IV's number of save slots (<c>_diskmenu</c>'s three).</summary>
    private const int DiskSlots = 3;

    /// <summary>Opens the save page (<c>_disk(1)</c>); SPACE or a save goes back to <paramref name="back"/>.</summary>
    private void OpenSavePage(P4SystemPage? back)
    {
        _diskBack = back;
        _system = P4SystemPage.Save;
        Cursor = 0;
    }

    /// <summary>F9 with the original's slots: the load page over the slots that hold a save, or a note when none does.</summary>
    private void OpenLoadPage()
    {
        if (!Enumerable.Range(1, DiskSlots).Any(SlotSaved))
        {
            Note = UiText.S("p4.dungeon.no_save");
            return;
        }
        _diskBack = _system;
        _system = P4SystemPage.Load;
        Cursor = 0;
    }

    /// <summary>The slot a digit key names (top row or keypad), or null.</summary>
    private static int? SlotKey(Keys key) => key switch
    {
        >= Keys.D1 and <= Keys.D3 => key - Keys.D0,
        >= Keys.NumPad1 and <= Keys.NumPad3 => key - Keys.NumPad0,
        _ => null,
    };

    /// <summary>
    /// The save or load page's panel from the pack's <c>"disk"</c> (s151 data, P4.X <c>_diskmenu</c>): Save = SPACE at
    /// place 0, then all three slot icons (their marks drawn by <see cref="DiskLabel"/>); Load = SPACE, then the slots that
    /// hold a save at the next places, as <c>_disk(0)</c> places them. Null without the pack's panel.
    /// </summary>
    private CommandPanel? DiskPanel(string name) => DiskPanel(Assets?.Pack?.CommandPanels, name, SlotSaved);

    /// <summary><see cref="DiskPanel(string)"/> from <paramref name="set"/> with <paramref name="saved"/> saying which slots hold a save.</summary>
    public static CommandPanel? DiskPanel(CommandPanelSet? set, string name, Func<int, bool> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        if (name is not (SavePanelName or LoadPanelName) || set is not { DiskMenu: { } disk, DiskSpace: { } space })
            return null;
        var slots = disk.Icons.Where(i => TitleSplash.SlotOf(i) is int k && (name == SavePanelName || saved(k)));
        return new CommandPanel(name, disk.Title, disk.TitleText, [space, .. slots]);
    }

    private string DiskLabel(CommandPanel panel, CommandPanelIcon icon, string label) => DiskLabel(panel, icon, label, SlotSaved);

    /// <summary>
    /// A label of the save page: <c>_diskmenu(1)</c> writes '*' (saved) or ' ' over the slot label's first character;
    /// every other label as it is.
    /// </summary>
    public static string DiskLabel(CommandPanel panel, CommandPanelIcon icon, string label, Func<int, bool> saved)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(saved);
        return panel.Name == SavePanelName && TitleSplash.SlotOf(icon) is int k && label.Length > 0
            ? (saved(k) ? '*' : ' ') + label[1..]
            : label;
    }

    private void OpenSystem()
    {
        _system = P4SystemPage.Menu;
        Cursor = 0;
    }

    /// <summary>s158: <c>_acgamend(1)</c> (the final screen, a wiped-out party): End game / Back to title, no way back.</summary>
    private void OpenSelectGameFinal()
    {
        _system = _selectGame = P4SystemPage.SelectGameFinal;
        Cursor = 0;
    }

    /// <summary>A key on the System screen; the shell request it makes (save, main menu, title), or null.</summary>
    private P4ScreenRequest? SystemKey(Keys key)
    {
        switch (_system)
        {
            case P4SystemPage.Menu:
                switch (key)
                {
                    case Keys.Space:
                        _system = null;
                        break;
                    case Keys.S when !Game.CanSave:
                        Note = UiText.S("p4.town.no_save_inside");
                        break;
                    case Keys.S when OriginalSlots:
                        OpenSavePage(P4SystemPage.Menu); // _syscon's S: _disk(1), then the System icons again
                        break;
                    case Keys.S:
                        _system = null;
                        return P4ScreenRequest.Save;
                    case Keys.E:
                        _system = _selectGame = P4SystemPage.SelectGame; // _syscon's E: _acgamend(0)
                        break;
                    case Keys.A:
                        _system = P4SystemPage.About;
                        break;
                    case Keys.D or Keys.B:
                        Note = UiText.S("p4.system.not_built");
                        break;
                }
                break;
            case P4SystemPage.SelectGame or P4SystemPage.SelectGameFinal:
                if (key == Keys.E)
                    _system = P4SystemPage.EndGame;
                else if (key == Keys.T)
                    _system = P4SystemPage.BackToTitle;
                else if (key == Keys.Space && _system == P4SystemPage.SelectGame)
                    _system = P4SystemPage.Menu;
                break;
            case P4SystemPage.EndGame or P4SystemPage.BackToTitle:
                if (key is Keys.Y or Keys.O)
                {
                    var title = _system == P4SystemPage.BackToTitle;
                    _system = null;
                    return title ? P4ScreenRequest.Title : P4ScreenRequest.MainMenu;
                }
                if (key is Keys.N or Keys.C or Keys.Space)
                    _system = _selectGame; // _acend's C: back to _acgamend's icons
                break;
            case P4SystemPage.About when key == Keys.Space:
                _system = P4SystemPage.Menu;
                break;
            case P4SystemPage.Save or P4SystemPage.Load when key == Keys.Space:
                _system = _diskBack;
                break;
            // _disk(1): one _mgetc; a slot's button saves there (no question over a saved slot), then back.
            case P4SystemPage.Save when SlotKey(key) is int slot:
                _system = _diskBack;
                RequestedSlot = slot;
                return P4ScreenRequest.SaveSlot;
            case P4SystemPage.Load when SlotKey(key) is int slot && SlotSaved(slot):
                _system = _diskBack;
                RequestedSlot = slot;
                return P4ScreenRequest.LoadSlot;
        }
        return null;
    }

    private List<MenuLine> SystemLines()
    {
        var panel = DiskPanel(SystemPanel(_system!.Value))
            ?? Assets?.Pack?.CommandPanels?.Panels.GetValueOrDefault(SystemPanel(_system!.Value));
        var lines = new List<MenuLine>();
        if (panel is not null)
            lines.Add(new(PanelText(panel.Title, panel.TitleText), MenuLineStyle.Title));
        switch (_system)
        {
            case P4SystemPage.About:
                lines.AddRange(AboutLines.Select(id => new MenuLine(GameText.S(id), MenuLineStyle.Normal)));
                break;
            case P4SystemPage.EndGame:
                lines.Add(new(UiText.S("p4.system.end_question"), MenuLineStyle.Normal));
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4SystemPage.BackToTitle:
                lines.Add(new(UiText.S("p4.system.title_question"), MenuLineStyle.Normal));
                lines.Add(new(UiText.S("p4.outdoor.yes_no"), MenuLineStyle.Selected));
                break;
            case P4SystemPage.SelectGame or P4SystemPage.SelectGameFinal:
                // The panel holds the choices; without a pack they are listed here.
                if (panel is null)
                    lines.Add(new(SystemHelp, MenuLineStyle.Normal));
                // The final screen's window stays up under _acgamend(1) (_endstory draws nothing over it).
                if (_system == P4SystemPage.SelectGameFinal && Game.Place == P4Place.Town && W.Prompt == P4TownPrompt.Ending)
                    lines.AddRange(W.Box.Select(l => new MenuLine(l, MenuLineStyle.Normal)));
                break;
            case P4SystemPage.Save or P4SystemPage.Load:
                // The pack's panel holds the slots; without it they are listed here.
                if (!DiskPanelShown)
                    for (var k = 1; k <= DiskSlots; k++)
                        if (_system == P4SystemPage.Save || SlotSaved(k))
                            lines.Add(new(UiText.F("p4.system.slot_xy", SlotSaved(k) ? "*" : " ", k), MenuLineStyle.Normal));
                break;
            default:
                if (panel is null)
                    lines.Add(new(UiText.S("p4.system.help"), MenuLineStyle.Normal));
                break;
        }
        return lines;
    }

    private string SystemHelp => _system switch
    {
        P4SystemPage.EndGame or P4SystemPage.BackToTitle => UiText.S("p4.outdoor.help_confirm"),
        P4SystemPage.About => UiText.S("p4.dungeon.help_wait"),
        P4SystemPage.SelectGame => UiText.S("p4.system.select_help"),
        P4SystemPage.SelectGameFinal => UiText.S("p4.system.select_final_help"),
        P4SystemPage.Save => UiText.S("p4.system.save_help"),
        P4SystemPage.Load => UiText.S("p4.system.load_help"),
        _ => UiText.S("p4.system.help"),
    };
}
