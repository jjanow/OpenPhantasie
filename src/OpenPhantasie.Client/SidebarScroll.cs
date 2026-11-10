using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// s172 (PLAN P3, user decision on the II terrain key): keyboard and pad scrolling of the field sidebar's two scroll areas
/// beside the wilderness / dungeon map, which before took only the mouse wheel and the bar: the party cards
/// (<see cref="UiOverlays.FieldPartyScroll"/>: II's cards under the terrain key, I / III with a summoned elemental) and II's
/// terrain key (<see cref="UiOverlays.TerrainKeyScroll"/>). A row a press.
/// <list type="bullet">
/// <item><c>[</c> / <c>]</c> the party cards (the remaster's "scroll the side box" keys in Options and the inventory);
/// <c>,</c> / <c>.</c> the terrain key. Not the arrows, Home / End / PgUp / PgDn (field steps, the diagonals switch), nor
/// III's outside-bar keys (<c>+ = - ? / P</c>, <see cref="AmigaOutsideBar.ForKey"/>).</item>
/// <item>Pad: the right stick up / down the party cards, with the right trigger held the terrain key (the D-pad and left
/// stick step, the stick on a slant with DIAGONAL MOVES on, the stick clicks turn, the left trigger fast-forwards).</item>
/// </list>
/// </summary>
public static class SidebarScroll
{
    /// <summary>Which of the sidebar's scroll areas a key or button moves.</summary>
    public enum Area
    {
        PartyCards,
        TerrainKey,
    }

    /// <summary>The area and direction (−1 up, +1 down) <paramref name="key"/> scrolls, or null.</summary>
    public static (Area Area, int Rows)? ForKey(Keys key) => key switch
    {
        Keys.OemOpenBrackets => (Area.PartyCards, -1),
        Keys.OemCloseBrackets => (Area.PartyCards, 1),
        Keys.OemComma => (Area.TerrainKey, -1),
        Keys.OemPeriod => (Area.TerrainKey, 1),
        _ => null,
    };

    /// <summary>The area and direction a newly pressed pad <paramref name="button"/> scrolls, or null.</summary>
    public static (Area Area, int Rows)? ForButton(Buttons button, bool rightTriggerHeld) => button switch
    {
        Buttons.RightThumbstickUp => (rightTriggerHeld ? Area.TerrainKey : Area.PartyCards, -1),
        Buttons.RightThumbstickDown => (rightTriggerHeld ? Area.TerrainKey : Area.PartyCards, 1),
        _ => null,
    };

    /// <summary>The right trigger counts as held past half way (as the left one does for fast-forward).</summary>
    public static bool RightTriggerHeld(GamePadState pad) => pad.Triggers.Right > 0.5f;

    /// <summary>
    /// Scrolls <paramref name="party"/> / <paramref name="key"/> by this frame's new <paramref name="keys"/> and
    /// <paramref name="buttons"/>; true when one of them was a scroll input (the box clamps; one that cannot scroll stays).
    /// </summary>
    public static bool Apply(IEnumerable<Keys> keys, IEnumerable<Buttons> buttons, bool rightTriggerHeld,
        ScrollBox party, ScrollBox key)
    {
        var took = false;
        foreach (var step in keys.Select(ForKey).Concat(buttons.Select(b => ForButton(b, rightTriggerHeld))))
        {
            if (step is not var (area, rows))
                continue;
            (area == Area.PartyCards ? party : key).ScrollBy(rows);
            took = true;
        }
        return took;
    }

    /// <summary>A key of <see cref="ForKey"/>: the remaster's own, not a field command (II ST's field click skips it).</summary>
    public static bool IsScrollKey(Keys key) => ForKey(key) is not null;
}
