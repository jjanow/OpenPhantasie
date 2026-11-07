using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s155: I Amiga's menu bar over the map screens (pack <c>map_message.menu_bar</c>: Info / Options / Speed, h80
/// <c>+0xb48</c>). The Amiga pulls a menu down while the right button is held; the remaster opens it with a click on its
/// title (a second click, Esc or a click elsewhere closes it) and takes an item with a click. Each item's pack action names
/// the remaster command it runs (<see cref="CombatFieldMenu.Actions"/>).
/// </summary>
public sealed class P1MapMenuBar
{
    /// <summary>The menu pulled down, or null.</summary>
    public int? Open { get; private set; }

    /// <summary>The highlighted item of the open menu, or −1 (none: the pointer is not on an item).</summary>
    public int Item { get; private set; } = -1;

    /// <summary>A click on title <paramref name="menu"/> of <paramref name="bar"/>: pulls it down, or closes it when open.</summary>
    public void Toggle(CombatFieldMenuBar bar, int menu)
    {
        if (menu < 0 || menu >= bar.Menus.Count)
            return;
        Open = Open == menu ? null : menu;
        Item = -1;
    }

    /// <summary>The pointer over item <paramref name="item"/> of the open menu highlights it.</summary>
    public void Hover(CombatFieldMenuBar bar, int item)
    {
        if (Open is { } menu)
            Item = item >= 0 && item < bar.Menus[menu].Items.Count ? item : -1;
    }

    /// <summary>A click on item <paramref name="item"/>: the menu closes and the item's action is returned ("" = none).</summary>
    public string Take(CombatFieldMenuBar bar, int item)
    {
        if (Open is not { } menu || item < 0 || item >= bar.Menus[menu].Items.Count)
            return "";
        Close();
        var actions = bar.Menus[menu].Actions;
        return item < actions.Count ? actions[item] : "";
    }

    public void Close()
    {
        Open = null;
        Item = -1;
    }

    /// <summary>Whether item <paramref name="item"/> of menu <paramref name="menu"/> is ghosted (and inert) here.</summary>
    public static bool Ghosted(CombatFieldMenuBar bar, int menu, int item, bool outdoors) =>
        outdoors && menu >= 0 && menu < bar.Menus.Count && item >= 0 && item < bar.Menus[menu].Actions.Count
        && bar.GhostedOutdoors.Contains(bar.Menus[menu].Actions[item]);

    /// <summary>The speed a Speed item's action sets, or null.</summary>
    public static AmigaCombatSpeed? SpeedOf(string action) => action switch
    {
        "fast" => AmigaCombatSpeed.Fast,
        "normal" => AmigaCombatSpeed.Normal,
        "slow" => AmigaCombatSpeed.Slow,
        _ => null,
    };
}
