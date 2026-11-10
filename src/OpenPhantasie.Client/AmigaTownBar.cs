using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s156: III Amiga's town bar (menu list <c>0x238a4</c>, FORMAT.md 8.5 / 8.9): File (Info, Load, Save), Guild (Add, Drop,
/// Examine, New, Purge, Rename, a rule, Learn Spells, Training) and Options (Armoury, Bank, Inn, Mystic, Exit, a rule, Cast
/// Spell, Distribute, Trade Item, Use Item, View Party), places and actions from the pack (<c>keep_windows.menu_bars.town</c>).
/// The Amiga pulls a menu down while the button is held over the bar and takes the row it is released on; the remaster
/// does that (press-drag-release) and also click, click (a click on a title pulls it down, a click on an item takes it).
/// The remaster's arrows pull Options down and move the highlight (Left / Right: the next menu), Return / Space take it,
/// Esc closes it. A rule row has no action (its key in <c>0x2189c</c> is 0).
/// </summary>
public sealed class AmigaTownBar
{
    /// <summary>The Options menu's index (the arrows and Return pull it down first, as the fight's bar).</summary>
    public const int OptionsMenu = 2;

    /// <summary>The menu pulled down, or null.</summary>
    public int? Open { get; private set; }

    /// <summary>The highlighted item of the open menu, or −1 (none).</summary>
    public int Item { get; private set; } = -1;

    /// <summary>A click (or press) on title <paramref name="menu"/>: pulls it down, or closes it when it is the open one.</summary>
    public void Toggle(CombatFieldMenuBar bar, int menu)
    {
        if (menu < 0 || menu >= bar.Menus.Count)
            return;
        if (Open == menu)
            Close();
        else
            PullDown(menu);
    }

    /// <summary>Pulls menu <paramref name="menu"/> down with nothing lit (the pointer is on the title).</summary>
    public void PullDown(int menu)
    {
        Open = menu;
        Item = -1;
    }

    /// <summary>The pointer over item <paramref name="item"/> of the open menu highlights it (−1: none).</summary>
    public void Hover(CombatFieldMenuBar bar, int item)
    {
        if (Open is { } menu && menu < bar.Menus.Count)
            Item = item >= 0 && item < bar.Menus[menu].Items.Count ? item : -1;
    }

    /// <summary>Item <paramref name="item"/> taken: the menu closes and the item's action is returned ("" = none).</summary>
    public string Take(CombatFieldMenuBar bar, int item)
    {
        if (Open is not { } menu || menu >= bar.Menus.Count || item < 0 || item >= bar.Menus[menu].Items.Count)
        {
            Close();
            return "";
        }
        Close();
        var actions = bar.Menus[menu].Actions;
        return item < actions.Count ? actions[item] : "";
    }

    public void Close()
    {
        Open = null;
        Item = -1;
    }

    /// <summary>
    /// The bar's own keys (key table <c>0x2189c</c>, town rows 0–2: <c>?OS</c> / <c>+-ENPR·LT</c> / <c>ABIMX·CDGUV</c>,
    /// FORMAT.md 8.5), as the pack's actions: what each item's label shows ("G) Trade Item"). D3-074 (user s180): under
    /// AMIGA SCREENS a letter does what the bar says, so G is Trade, I the Inn, N New Member, L Learn Spells, T Training and
    /// X Exit, not the manual's town letters (D-066). <c>/</c> is the numeric pad's ?, as outside the towns. "" = no item.
    /// </summary>
    public static string ActionForKey(Keys key) => key switch
    {
        Keys.OemQuestion or Keys.Divide => "info",
        Keys.O => "load",
        Keys.S => "save",
        Keys.OemPlus or Keys.Add => "add",
        Keys.OemMinus or Keys.Subtract => "drop",
        Keys.E => "examine",
        Keys.N => "new",
        Keys.P => "purge",
        Keys.R => "rename",
        Keys.L => "learn",
        Keys.T => "training",
        Keys.A => "armoury",
        Keys.B => "bank",
        Keys.I => "inn",
        Keys.M => "mystic",
        Keys.X => "exit",
        Keys.C => "cast",
        Keys.D => "distribute",
        Keys.G => "trade",
        Keys.U => "use",
        Keys.V => "view",
        _ => "",
    };

    /// <summary>
    /// The remaster's keys on the bar: Up / Down pull Options down (on its first row) or move the highlight (wrapping),
    /// Left / Right step to the next menu, Return / Space take the lit row (or pull Options down), Esc closes an open menu.
    /// Returns (used, action): used = the key belongs to the bar; action = what a taken row does ("" none).
    /// </summary>
    public (bool Used, string Action) HandleKey(CombatFieldMenuBar bar, Keys key)
    {
        switch (key)
        {
            case Keys.Escape when Open is not null:
                Close();
                return (true, "");
            case Keys.Up or Keys.Down or Keys.Enter or Keys.Space when Open is null:
                Open = Math.Min(OptionsMenu, bar.Menus.Count - 1);
                Item = 0;
                return (true, "");
            case Keys.Up or Keys.Down when Open is { } open:
                var count = bar.Menus[open].Items.Count;
                Item = Item < 0 ? 0 : (Item + (key == Keys.Up ? count - 1 : 1)) % count;
                return (true, "");
            case Keys.Left or Keys.Right when Open is { } open:
                Open = (open + (key == Keys.Left ? bar.Menus.Count - 1 : 1)) % bar.Menus.Count;
                Item = 0;
                return (true, "");
            case Keys.Left or Keys.Right:
                return (true, ""); // the DOS row's cursor is not on this screen
            case Keys.Enter or Keys.Space when Open is not null:
                return (true, Item < 0 ? "" : Take(bar, Item));
            default:
                return (false, "");
        }
    }

    /// <summary>
    /// "Pad coverage" (s182): the pad's Start pulls Options down (as Return does) or closes the open menu; the D-pad, A and B
    /// reach <see cref="HandleKey"/> as the arrows, Return and Esc (<see cref="InputMap.MenuPadKeys"/>). True when used.
    /// </summary>
    public bool HandleButton(CombatFieldMenuBar bar, Buttons button)
    {
        if (button != Buttons.Start || bar.Menus.Count == 0)
            return false;
        if (Open is null)
            HandleKey(bar, Keys.Enter);
        else
            Close();
        return true;
    }
}
