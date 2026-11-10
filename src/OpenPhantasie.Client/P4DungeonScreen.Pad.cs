using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// "Pad coverage" (s182): IV's screens read keys only (<see cref="HandleKeys"/>), and IV gives Return, Space and Esc
/// different meanings from screen to screen (Space is back in a pick list and closes the System menu, Esc asks for the
/// main menu), so the pad's keys follow the prompt up now instead of <see cref="InputMap.MenuPadKeys"/>' fixed map:
/// <list type="bullet">
/// <item>A: Y at a yes / no question, Space on the System's About page, the chosen order in a fight's orders (a blow, else
/// Parry, until X / Y picks another), else Return (every SPACE wait, list pick, typed prompt's end, the target's square).</item>
/// <item>B: N at a question, Space where a list or the System page goes back, Backspace in a fight's target / spell /
/// member pick and in a typed prompt, else Esc (the main-menu question).</item>
/// <item>D-pad / left stick: the arrows (the cursor, a step); on a map or in a fight's orders the perpendicular held with
/// it gives the diagonal (Home / PgUp / End / PgDn, <see cref="ButtonOf"/>).</item>
/// <item>LB / RB: F5 / F9 (save / load, as <see cref="GameAction.Save"/> / <see cref="GameAction.Load"/> in I–III; never
/// PgUp / PgDn, which are moves here). Start: Esc.</item>
/// <item>Y / X: the next / previous button the mouse could click (the Command panel's icons, the SPACE bar, the keypad, a
/// building's or an item window's buttons), lit as under the pointer; A then clicks it, B lets it go. In a fight's orders
/// (no buttons there) they pick the order A gives.</item>
/// </list>
/// Keyboard keys are untouched; the pad only adds keys.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private int? _padFocus;
    private (object? Tag, Rectangle Bounds)[] _padSeen = [];
    private P4ActionKind? _padAction;

    /// <summary>The middle of the button the pad has picked (Y / X), or null; the shell lights it as under the pointer.</summary>
    public Point? PadPointer { get; private set; }

    /// <summary>The fight order the pad's A gives (X / Y pick it), or null before the pad picked one.</summary>
    public P4ActionKind? PadAction => _padAction;

    /// <summary>Lets go of the pad's picked button (the mouse moved).</summary>
    public void ClearPadFocus()
    {
        _padFocus = null;
        PadPointer = null;
    }

    /// <summary>A region the pad can pick: one of IV's own buttons (a Command panel icon and its kin, a keypad key).</summary>
    public static bool IsPadTarget(HitRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Tag switch
        {
            P4PanelIconTag => region.Click.Count > 0,
            P4KeypadTag k => k.Digit is not null || region.Click.Count > 0,
            _ => false,
        };
    }

    /// <summary>The regions of <paramref name="regions"/> the pad can pick, in drawing order.</summary>
    public static IReadOnlyList<HitRegion> PadTargets(IEnumerable<HitRegion> regions) =>
        [.. regions.Where(IsPadTarget)];

    /// <summary>
    /// The pad's pick after one Y (<paramref name="step"/> 1) or X (−1) among <paramref name="count"/> buttons: the next /
    /// previous one, wrapping; from none, the first / last. Null when there is none.
    /// </summary>
    public static int? StepFocus(int? current, int count, int step)
    {
        if (count <= 0)
            return null;
        if (current is not int c || c >= count)
            return step >= 0 ? 0 : count - 1;
        return ((c + step) % count + count) % count;
    }

    /// <summary>The pad buttons held down in <paramref name="pad"/>.</summary>
    public static IReadOnlyCollection<Buttons> HeldButtons(GamePadState pad) =>
        [.. Enum.GetValues<Buttons>().Where(pad.IsButtonDown)];

    /// <summary>
    /// This frame's pad input as IV's keys (to follow the keyboard's into <see cref="HandleKeys"/>): <paramref name="pressed"/>
    /// are the buttons newly down, <paramref name="held"/> all buttons down (for the diagonals), <paramref name="regions"/>
    /// the last drawing's click targets. <paramref name="press"/> is the picked button when A clicks it (the shell acts on
    /// it as on a mouse click), else null.
    /// </summary>
    public IReadOnlyList<Keys> PadKeys(IReadOnlyCollection<Buttons> pressed, IReadOnlyCollection<Buttons> held,
        IReadOnlyList<HitRegion> regions, out HitRegion? press)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(regions);
        press = null;
        var targets = PadTargets(regions);
        var seen = targets.Select(t => (t.Tag, t.Bounds)).ToArray();
        if (!seen.SequenceEqual(_padSeen))
            _padFocus = null; // another set of buttons: the old pick would press something else
        _padSeen = seen;
        var keys = new List<Keys>();
        if (!(DoorOpen || MysticBusy || WelcomeUp)) // the busy waits read nothing (HandleKeys)
        {
            foreach (var b in pressed)
            {
                switch (b)
                {
                    case Buttons.Y or Buttons.X:
                        var step = b == Buttons.Y ? 1 : -1;
                        if (PadOrdersUp is { } fight)
                            CyclePadAction(fight, step);
                        else if (!MainMenuAsked)
                            _padFocus = StepFocus(_padFocus, targets.Count, step);
                        break;
                    case Buttons.A when _padFocus is int f && !MainMenuAsked:
                        press ??= targets[f];
                        break;
                    case Buttons.A:
                        keys.Add(PadConfirmKey());
                        break;
                    case Buttons.B when _padFocus is not null && !MainMenuAsked:
                        _padFocus = null;
                        break;
                    case Buttons.B:
                        keys.Add(PadBackKey());
                        break;
                    case Buttons.LeftShoulder:
                        keys.Add(Keys.F5);
                        break;
                    case Buttons.RightShoulder:
                        keys.Add(Keys.F9);
                        break;
                    case Buttons.Start:
                        keys.Add(Keys.Escape);
                        break;
                }
            }
            keys.AddRange(PadMoves(pressed, held, PadDiagonals));
        }
        PadPointer = _padFocus is int i ? targets[i].Bounds.Center : null;
        return keys;
    }

    private static readonly Buttons[] DPad = [Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight];

    private static readonly Buttons[] Stick =
        [Buttons.LeftThumbstickUp, Buttons.LeftThumbstickDown, Buttons.LeftThumbstickLeft, Buttons.LeftThumbstickRight];

    /// <summary>
    /// The arrows for the D-pad's and the left stick's new presses (up, down, left, right). With
    /// <paramref name="diagonals"/>, a new press while the perpendicular of the same control is down gives the diagonal
    /// key instead (Home up-left, PgUp up-right, End down-left, PgDn down-right: <see cref="ButtonOf"/>), one per control.
    /// </summary>
    public static IEnumerable<Keys> PadMoves(IReadOnlyCollection<Buttons> pressed, IReadOnlyCollection<Buttons> held, bool diagonals)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(held);
        Keys[] arrows = [Keys.Up, Keys.Down, Keys.Left, Keys.Right];
        foreach (var control in new[] { DPad, Stick })
        {
            if (!control.Any(pressed.Contains))
                continue;
            bool Down(int n) => held.Contains(control[n]) || pressed.Contains(control[n]);
            var dy = (Down(1) ? 1 : 0) - (Down(0) ? 1 : 0);
            var dx = (Down(3) ? 1 : 0) - (Down(2) ? 1 : 0);
            if (diagonals && dx != 0 && dy != 0)
            {
                yield return (dx, dy) switch
                {
                    (< 0, < 0) => Keys.Home,
                    (> 0, < 0) => Keys.PageUp,
                    (< 0, > 0) => Keys.End,
                    _ => Keys.PageDown,
                };
                continue;
            }
            for (var n = 0; n < control.Length; n++)
                if (pressed.Contains(control[n]))
                    yield return arrows[n];
        }
    }

    /// <summary>The final screen's save question is up (as <see cref="HandleKeys"/> tests it).</summary>
    private bool EndingQuestionUp => _system is null && Game.Battle is null && Game.Place == P4Place.Town
        && W.Prompt == P4TownPrompt.Ending && W.EndingStep == P4EndingStep.Question;

    /// <summary>The System screen takes the keys (as <see cref="HandleKeys"/> tests it).</summary>
    private bool SystemUp => _system is not null && (Game.Battle is null || DiskPageUp);

    /// <summary>The fight whose orders are asked with no target, spell or member pick open; else null.</summary>
    private P4Battle? PadOrdersUp => !MainMenuAsked && Game.LoopBuilt && !SystemUp
        && Game.Battle is { Prompt: P4BattlePrompt.Order, Current: not null } b && !b.AutoCombat && _mode == BattleMode.Normal
            ? b : null;

    /// <summary>The D-pad steps here (a map's action prompt, a fight's orders or target), so its diagonals count.</summary>
    private bool PadDiagonals => !MainMenuAsked && Game.LoopBuilt && !SystemUp && !EndingQuestionUp
        && (Game.Battle is { } b
            ? b.Prompt == P4BattlePrompt.Order && _mode is BattleMode.Normal or BattleMode.Target
            : !MenuOpen && Game.Place switch
            {
                P4Place.Outdoors => O.Prompt == P4OutdoorPrompt.Action,
                P4Place.Dungeon => D.Prompt == P4DungeonPrompt.Action,
                _ => false,
            });

    /// <summary>The key the pad's A stands for now.</summary>
    private Keys PadConfirmKey()
    {
        if (MainMenuAsked)
            return Keys.Y;
        if (!Game.LoopBuilt)
            return Keys.Enter;
        if (EndingQuestionUp)
            return Keys.Y;
        if (SystemUp)
            return _system switch
            {
                P4SystemPage.EndGame or P4SystemPage.BackToTitle => Keys.Y,
                P4SystemPage.About => Keys.Space,
                _ => Keys.Enter,
            };
        if (Game.Battle is { } b)
        {
            if (b.Prompt == P4BattlePrompt.Treasure)
                return Keys.Y;
            if (PadOrdersUp is { } fight && PadOrderKey(fight) is { } order)
                return order;
            return Keys.Enter;
        }
        if (MenuOpen)
            return M!.Prompt == P4MenuPrompt.Confirm ? Keys.Y : Keys.Enter;
        return Game.Place switch
        {
            P4Place.Town when W.Prompt == P4TownPrompt.Confirm => Keys.Y,
            P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Confirm => Keys.Y,
            _ => Keys.Enter,
        };
    }

    /// <summary>The key the pad's B stands for now.</summary>
    private Keys PadBackKey()
    {
        if (MainMenuAsked)
            return Keys.N;
        if (!Game.LoopBuilt)
            return Keys.Escape;
        if (EndingQuestionUp)
            return Keys.N;
        if (SystemUp)
            return _system switch
            {
                P4SystemPage.EndGame or P4SystemPage.BackToTitle => Keys.N,
                P4SystemPage.SelectGameFinal => Keys.Escape, // no way back: the main-menu question
                _ => Keys.Space,
            };
        if (Game.Battle is { } b)
            return b.Prompt switch
            {
                P4BattlePrompt.Treasure => Keys.N,
                P4BattlePrompt.Order when _mode != BattleMode.Normal => Keys.Back,
                _ => Keys.Escape,
            };
        if (MenuOpen)
            return M!.Prompt switch
            {
                P4MenuPrompt.Pick or P4MenuPrompt.Who when M.CanBack => Keys.Space,
                P4MenuPrompt.Confirm => Keys.N,
                _ => Keys.Escape,
            };
        return Game.Place switch
        {
            P4Place.Town => W.Prompt switch
            {
                P4TownPrompt.Pick or P4TownPrompt.Who when W.CanBack => Keys.Space,
                P4TownPrompt.Confirm => Keys.N,
                P4TownPrompt.Number or P4TownPrompt.Text => Keys.Back,
                _ => Keys.Escape,
            },
            P4Place.Outdoors when O.Prompt == P4OutdoorPrompt.Confirm => Keys.N,
            P4Place.Dungeon when D.Prompt is P4DungeonPrompt.Letters or P4DungeonPrompt.Number => Keys.Back,
            _ => Keys.Escape,
        };
    }

    /// <summary>The orders the pad can give in <paramref name="b"/>: the offered ones that have a key.</summary>
    private static List<P4ActionKind> PadOrders(P4Battle b) => [.. b.Offered().Where(ActionKeys.ContainsValue)];

    /// <summary>
    /// The order A gives before the pad picks one: the first of these offered (a blow when a foe is in reach, else Parry,
    /// so A never opens the spell list unasked).
    /// </summary>
    public static readonly IReadOnlyList<P4ActionKind> PadOrderDefaults =
    [
        P4ActionKind.Attack, P4ActionKind.Thrust, P4ActionKind.Slash, P4ActionKind.Lunge, P4ActionKind.AimBlow,
        P4ActionKind.FireBow, P4ActionKind.Throw, P4ActionKind.Parry,
    ];

    /// <summary>The pad's order now: its pick when offered, else the first of <see cref="PadOrderDefaults"/> offered, else the
    /// first offered; null with none.</summary>
    private P4ActionKind? PadOrderNow(List<P4ActionKind> offered) =>
        offered.Count == 0 ? null
        : _padAction is { } k && offered.Contains(k) ? k
        : PadOrderDefaults.Where(offered.Contains).Cast<P4ActionKind?>().FirstOrDefault() ?? offered[0];

    /// <summary>The order key A gives in <paramref name="b"/>'s orders (<see cref="PadOrderNow"/>; it stays the pad's pick).</summary>
    private Keys? PadOrderKey(P4Battle b)
    {
        if (PadOrderNow(PadOrders(b)) is not { } kind)
            return null;
        _padAction = kind;
        return ActionKeys.First(p => p.Value == kind).Key;
    }

    /// <summary>X / Y in the orders: the previous / next offered order becomes the pad's pick.</summary>
    private void CyclePadAction(P4Battle b, int step)
    {
        var offered = PadOrders(b);
        var at = PadOrderNow(offered) is { } now ? offered.IndexOf(now) : -1;
        if (StepFocus(at >= 0 ? at : null, offered.Count, step) is int n)
            _padAction = offered[n];
    }
}
