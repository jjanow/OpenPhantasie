using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>The OK window III Amiga's bar has up: none, the credits (window 0) or the Party Status (window 22).</summary>
public enum OutsideBarWindow { None, Credits, PartyStatus }

/// <summary>What an item of III Amiga's bar outside the towns does in the remaster.</summary>
public enum OutsideBarAct { None, Info, Cast, Examine, PartyStatus, UseItem, Fast, Normal, Slow }

/// <summary>
/// s136: III Amiga's menu bar outside the towns (menu list <c>0x23910</c>, FORMAT.md 8.5): drawn (<c>0x3894</c>) and
/// pulled down (<c>0x184a</c> while the button is down above y 11) by the overworld's main loop (<c>0x7be2</c> /
/// <c>0x7cc8</c>, once a visit: flag <c>0x24092</c>) and the dungeon routine (<c>0x1bce0</c> / <c>0x1bf92</c>). Its items'
/// keys go to the shared handler <c>0x83e0</c>: <c>?</c> Info (window 0, the credits), C Cast Spell (a member, then a
/// spell), E Examine Member, P Party Status (window 22), U Use Item, + = − the Speed word (<c>0x870e</c>, the fight's
/// too). The remaster draws it under AMIGA SCREENS from the pack's places (<c>keep_windows.menu_bars.outside</c>, s156);
/// the mouse as the Amiga (press on a title, drag, release on an item: <c>Game1.AmigaBarPress</c>, s156) or click, click
/// (a click on a title pulls its menu down, a second click closes it, a click on an item takes it). The items open the remaster's own
/// dialogs: Cast → cast, Examine → inspect, Use Item → use potion; Speed sets the game's COMBAT SPEED; Info opens the
/// credits (window 0, s140) and Party Status the Amiga's window 22, the trip summary (s141; the health dialog without it). Keys (s140, <c>0x83e0</c> / the dungeon's <c>0x1c052</c>):
/// ? and / Info, P Party Status, + = − the speed; C and U are the remaster's Cast / Use bindings already. The Amiga's E
/// is Examine Member (no letter moves the party there: the number pad's 8 / 2 / 4 / 6, the arrows and the mouse do), but
/// the III manual's E moves east, so E moves east by the user's decision (D3-070, manual).
/// </summary>
public sealed class AmigaOutsideBar
{
    /// <summary>The menus: title string id, then each item's act and label id (the Amiga's strings, blanks and all).</summary>
    public static readonly IReadOnlyList<(string TitleId, IReadOnlyList<(OutsideBarAct Act, string LabelId)> Items)> Menus =
    [
        ("p3field.menu.file", [(OutsideBarAct.Info, "p3field.item.info")]),
        ("p3field.menu.options",
        [
            (OutsideBarAct.Cast, "p3bar.item.cast"), (OutsideBarAct.Examine, "p3bar.item.examine"),
            (OutsideBarAct.PartyStatus, "p3bar.item.status"), (OutsideBarAct.UseItem, "p3bar.item.use"),
        ]),
        ("p3field.menu.speed", [(OutsideBarAct.Fast, "p3field.item.fast"), (OutsideBarAct.Normal, "p3field.item.normal"),
            (OutsideBarAct.Slow, "p3field.item.slow")]),
    ];

    /// <summary>The Speed menu's index; its items are Fast, Normal, Slow (<see cref="AmigaCombatSpeed"/> − 1).</summary>
    public const int SpeedMenu = 2;

    /// <summary>The menu pulled down, or null when the bar is closed.</summary>
    public int? Open { get; private set; }

    /// <summary>The highlighted item of the open menu.</summary>
    public int Item { get; private set; }

    /// <summary>A click on title <paramref name="menu"/>: pulls it down, or closes it when it is the open one.
    /// <paramref name="speed"/> is the setting in force (the Speed menu opens on it).</summary>
    public void Toggle(int menu, AmigaCombatSpeed speed)
    {
        if (menu < 0 || menu >= Menus.Count)
            return;
        Open = Open == menu ? null : menu;
        Item = menu == SpeedMenu ? (int)speed - 1 : 0;
    }

    /// <summary>The pointer over item <paramref name="item"/> of the open menu highlights it.</summary>
    public void Hover(int item)
    {
        if (Open is { } menu && item >= 0 && item < Menus[menu].Items.Count)
            Item = item;
    }

    /// <summary>A click on item <paramref name="item"/>: the menu closes and its act is returned.</summary>
    public OutsideBarAct Take(int item)
    {
        if (Open is not { } menu || item < 0 || item >= Menus[menu].Items.Count)
            return OutsideBarAct.None;
        Open = null;
        return Menus[menu].Items[item].Act;
    }

    /// <summary>Closes the open menu and the OK window (credits / Party Status).</summary>
    public void Close()
    {
        Open = null;
        Window = OutsideBarWindow.None;
    }

    /// <summary>Closes the open menu only (the bar went away; an OK window stays until its key).</summary>
    public void CloseMenu() => Open = null;

    /// <summary>
    /// The bar's keys outside the towns (<c>0x83e0</c>): + (numeric pad) Fast, = Normal, − Slow (<c>0x870e</c>); ? and /
    /// (the main row's key, the numeric pad's /) Info (<c>0x86e6</c>); P Party Status (<c>0x8832</c>). E (Examine on the
    /// Amiga) is not here: D3-070.
    /// </summary>
    public static OutsideBarAct ForKey(Keys key) => key switch
    {
        Keys.OemQuestion or Keys.Divide => OutsideBarAct.Info,
        Keys.P => OutsideBarAct.PartyStatus,
        Keys.Add => OutsideBarAct.Fast,
        Keys.OemPlus => OutsideBarAct.Normal,
        Keys.OemMinus or Keys.Subtract => OutsideBarAct.Slow,
        _ => OutsideBarAct.None,
    };

    /// <summary>
    /// The credits' and Party Status' wait (<c>0x3ba0</c> with key <c>'O'</c>, s140 / s141): O, Return or a click on OK (which
    /// sends O) close it, and so does C (the Amiga's unshifted c, the wait's second answer); Esc closes it too (the
    /// remaster's way back). Every other key is read and dropped while it is up.
    /// </summary>
    public static bool ClosesOkWindow(Keys key) => key is Keys.O or Keys.Enter or Keys.C or Keys.Escape;

    /// <summary>The OK window up: File › Info's credits (window 0, s140) or Options › Party Status (window 22, s141).</summary>
    public OutsideBarWindow Window { get; private set; }

    /// <summary>The credits window is up (File › Info, s140).</summary>
    public bool CreditsShown => Window == OutsideBarWindow.Credits;

    /// <summary>Window 22, the Party Status, is up (s141).</summary>
    public bool StatusShown => Window == OutsideBarWindow.PartyStatus;

    public void ShowCredits()
    {
        Open = null;
        Window = OutsideBarWindow.Credits;
    }

    public void ShowStatus()
    {
        Open = null;
        Window = OutsideBarWindow.PartyStatus;
    }

    /// <summary>While an OK window is up it takes every key; true when one was up (the keys are used up).</summary>
    public bool HandleWindowKeys(IEnumerable<Keys> newKeys)
    {
        if (Window == OutsideBarWindow.None)
            return false;
        if (newKeys.Any(ClosesOkWindow))
            Window = OutsideBarWindow.None;
        return true;
    }

    /// <summary>The Options menu (Cast, Examine, Party Status, Use Item): the one the pad's Start pulls down.</summary>
    public const int OptionsMenu = 1;

    /// <summary>
    /// "Pad coverage" (s182): the bar has no keys of its own to pull a menu down (the mouse and its letters only), so the pad
    /// gets one: Start pulls Options down (or closes the open menu); while a menu is down the D-pad / left stick Up / Down
    /// move the highlight (wrapping), Left / Right step to the next menu, A takes the lit item and B closes the menu. The
    /// keyboard's arrows keep moving the party (unchanged). Returns (used, act): used = the button belongs to the bar.
    /// </summary>
    public (bool Used, OutsideBarAct Act) HandleButton(Buttons button, AmigaCombatSpeed speed)
    {
        if (button == Buttons.Start)
        {
            if (Open is null)
                Toggle(OptionsMenu, speed);
            else
                CloseMenu();
            return (true, OutsideBarAct.None);
        }
        if (Open is not { } menu)
            return (false, OutsideBarAct.None);
        var key = InputMap.MenuPadKey(button);
        switch (key)
        {
            case Keys.Up or Keys.Down:
                var count = Menus[menu].Items.Count;
                Item = (Math.Clamp(Item, 0, count - 1) + (key == Keys.Up ? count - 1 : 1)) % count;
                return (true, OutsideBarAct.None);
            case Keys.Left or Keys.Right:
                var next = (menu + (key == Keys.Left ? Menus.Count - 1 : 1)) % Menus.Count;
                Open = next;
                Item = next == SpeedMenu ? (int)speed - 1 : 0;
                return (true, OutsideBarAct.None);
            case Keys.Enter:
                return (true, Take(Item));
            case Keys.Escape:
                CloseMenu();
                return (true, OutsideBarAct.None);
            default:
                return (false, OutsideBarAct.None);
        }
    }

    /// <summary>The speed an act sets, or null.</summary>
    public static AmigaCombatSpeed? SpeedOf(OutsideBarAct act) => act switch
    {
        OutsideBarAct.Fast => AmigaCombatSpeed.Fast,
        OutsideBarAct.Normal => AmigaCombatSpeed.Normal,
        OutsideBarAct.Slow => AmigaCombatSpeed.Slow,
        _ => null,
    };
}
