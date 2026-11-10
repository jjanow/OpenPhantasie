using Microsoft.Xna.Framework.Input;
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

    /// <summary>The Options menu (Inspect, Health, Cast Spell, Use potions, Look): the one the pad's Start pulls down.</summary>
    public const int OptionsMenu = 1;

    /// <summary>
    /// "Pad coverage" (s182): the strip has no keys of its own (the Amiga holds the right button; the remaster clicks), so
    /// the pad gets one: Start pulls Options down on its first live item (or closes the open menu); while a menu is down
    /// the D-pad / left stick Up / Down move the highlight past ghosted items (Look outdoors), wrapping, Left / Right step
    /// to the next menu, A takes the lit item (a ghosted or unlit one: nothing) and B closes it. Returns (used, action):
    /// used = the button belongs to the strip; action as <see cref="Take"/>.
    /// </summary>
    public (bool Used, string Action) HandleButton(CombatFieldMenuBar bar, Buttons button, bool outdoors)
    {
        if (bar.Menus.Count == 0)
            return (false, "");
        if (button == Buttons.Start)
        {
            if (Open is null)
                OpenOn(bar, Math.Min(OptionsMenu, bar.Menus.Count - 1), outdoors);
            else
                Close();
            return (true, "");
        }
        if (Open is not { } menu)
            return (false, "");
        var key = InputMap.MenuPadKey(button);
        switch (key)
        {
            case Keys.Up or Keys.Down:
                Item = NextLive(bar, menu, Item, key == Keys.Up ? -1 : 1, outdoors);
                return (true, "");
            case Keys.Left or Keys.Right:
                OpenOn(bar, (menu + (key == Keys.Left ? bar.Menus.Count - 1 : 1)) % bar.Menus.Count, outdoors);
                return (true, "");
            case Keys.Enter:
                return (true, Item >= 0 && !Ghosted(bar, menu, Item, outdoors) ? Take(bar, Item) : "");
            case Keys.Escape:
                Close();
                return (true, "");
            default:
                return (false, "");
        }
    }

    private void OpenOn(CombatFieldMenuBar bar, int menu, bool outdoors)
    {
        Open = menu;
        Item = NextLive(bar, menu, -1, 1, outdoors);
    }

    /// <summary>The next item after <paramref name="from"/> in <paramref name="step"/>'s direction that is not ghosted,
    /// wrapping (from −1: the first live one going down, the last going up); −1 when the menu has none.</summary>
    private static int NextLive(CombatFieldMenuBar bar, int menu, int from, int step, bool outdoors)
    {
        var count = bar.Menus[menu].Items.Count;
        var at = from < 0 ? (step > 0 ? -1 : 0) : from;
        for (var i = 0; i < count; i++)
        {
            at = ((at + step) % count + count) % count;
            if (!Ghosted(bar, menu, at, outdoors))
                return at;
        }
        return -1;
    }

    /// <summary>The speed a Speed item's action sets, or null.</summary>
    public static AmigaCombatSpeed? SpeedOf(string action) => action switch
    {
        "fast" => AmigaCombatSpeed.Fast,
        "normal" => AmigaCombatSpeed.Normal,
        "slow" => AmigaCombatSpeed.Slow,
        _ => null,
    };
}
