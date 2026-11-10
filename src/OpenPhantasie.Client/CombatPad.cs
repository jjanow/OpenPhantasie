using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// "Pad coverage" (s182): the pad in a fight (I–III, every layout), as keys for <see cref="CombatPresenter.HandleKey"/>.
/// Only the D-pad, the left stick, A and (s183) RT on a step that names a key for it (<see cref="CombatPadStep.RightTriggerKey"/>)
/// are read here: B (the game menu, or Esc / Backspace where a step backs out), X / Y (inventory / bestiary), LB / RB
/// (save / load), LT (fast-forward) and Back stay the game's own (<c>HandleGlobalInput</c>), so no button acts twice. On a step with picks (<see cref="CombatPadStep.Picks"/>) the D-pad steps a pick (wrapping; a first
/// Down / Right shows the first entry, a first Up / Left the last) that the help line names, and A sends its keys;
/// before a pick A sends the step's <see cref="CombatPadStep.AKey"/> (Return: keep the order, take the lit entry).
/// Elsewhere the D-pad is the arrows. It never sends Esc. s190 (pad fix list C12): the right stick up / down scrolls the
/// log (PgUp / PgDn), with RT held the monster-rank box (Home / End), as I–III's field sidebar takes it (the stick's left /
/// right is the pad's F2). On the two steps where RT is a key of its own (CANCEL / Thrust) its press still sends that key.
/// </summary>
public sealed class CombatPad
{
    private string? _stepId;

    /// <summary>The pick, 1-based; 0 = nothing picked on this step yet.</summary>
    public int Pick { get; private set; }

    /// <summary>The fight is not waiting for a key: the next step starts with no pick.</summary>
    public void Reset()
    {
        _stepId = null;
        Pick = 0;
    }

    /// <summary>The keys this frame's newly pressed <paramref name="buttons"/> send on <paramref name="step"/>.</summary>
    public IReadOnlyList<Keys> KeysFor(CombatPadStep? step, IEnumerable<Buttons> buttons, bool rightTriggerHeld = false)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        if (step is null)
        {
            Reset();
            return [];
        }
        Sync(step);
        var keys = new List<Keys>();
        var count = step.Picks.Count;
        foreach (var b in buttons)
        {
            var arrow = InputMap.MenuPadKey(b);
            if (arrow is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
            {
                var horizontal = arrow is Keys.Left or Keys.Right;
                if (count > 0 && (horizontal || !step.PickOnLeftRight))
                {
                    var delta = arrow is Keys.Up or Keys.Left ? -1 : 1;
                    Pick = Pick == 0 ? (delta < 0 ? count : 1) : (Pick - 1 + delta + count) % count + 1;
                }
                else if (horizontal && step.LeftRightKey != Keys.None)
                    keys.Add(step.LeftRightKey);
                else
                    keys.Add(arrow);
            }
            else if (ScrollKey(b, rightTriggerHeld) is var scroll and not Keys.None)
                keys.Add(scroll);
            else if (b == Buttons.RightTrigger)
            {
                if (step.RightTriggerKey != Keys.None)
                    keys.Add(step.RightTriggerKey);
            }
            else if (b == Buttons.A)
            {
                if (Pick > 0 && Pick <= count)
                    keys.AddRange(step.Picks[Pick - 1].Keys);
                else if (step.AFirstPick && count > 0)
                    keys.AddRange(step.Picks[0].Keys); // s192 (C19): Return does nothing on a digit-only step
                else
                    keys.Add(step.AKey);
                Pick = 0; // the step moves on (or takes the next pick fresh)
            }
        }
        return keys;
    }

    /// <summary>
    /// The right stick's scroll key (<see cref="CombatPresenter.HandleKey"/>): PgUp / PgDn the log, with RT held Home / End
    /// the rank box.
    /// </summary>
    public static Keys ScrollKey(Buttons button, bool rightTriggerHeld) => button switch
    {
        Buttons.RightThumbstickUp => rightTriggerHeld ? Keys.Home : Keys.PageUp,
        Buttons.RightThumbstickDown => rightTriggerHeld ? Keys.End : Keys.PageDown,
        _ => Keys.None,
    };

    /// <summary>
    /// The help line while the pad has picked on <paramref name="step"/>: the pick's number and label (a spell by
    /// <paramref name="spellName"/>), or null.
    /// </summary>
    public string? Help(CombatPadStep? step, Func<int, string>? spellName = null)
    {
        if (step is null || Pick == 0 || step.Id != _stepId || Pick > step.Picks.Count)
            return null;
        var pick = step.Picks[Pick - 1];
        var label = pick.SpellId < 0 ? pick.Label
            : spellName is null ? $"#{pick.SpellId + 1}" : $"#{pick.SpellId + 1} {spellName(pick.SpellId)}";
        return UiText.F("pad.event_pick_x_x", Pick, label);
    }

    /// <summary>The entry picked on <paramref name="step"/>, or null (nothing picked, or another step).</summary>
    public CombatPadPick? Picked(CombatPadStep? step) =>
        step is not null && Pick > 0 && step.Id == _stepId && Pick <= step.Picks.Count ? step.Picks[Pick - 1] : null;

    /// <summary>
    /// The clickable region <paramref name="pick"/> stands for on the frame just drawn (the one whose click sends the same
    /// keys: a member's box or row, an order button, a spell row, EXIT, Fight!), topmost first, or null.
    /// </summary>
    public static Rectangle? MarkFor(CombatPadPick? pick, HitMap? hits)
    {
        if (pick is null || hits is null)
            return null;
        for (var i = hits.Regions.Count - 1; i >= 0; i--)
            if (hits.Regions[i].Click.SequenceEqual(pick.Keys))
                return hits.Regions[i].Bounds;
        return null;
    }

    private void Sync(CombatPadStep step)
    {
        if (step.Id == _stepId)
            return;
        _stepId = step.Id;
        Pick = 0;
    }
}

public static partial class UiOverlays
{
    /// <summary>
    /// "Pad coverage" (s182): the pad's pick in a fight outlined (<see cref="CombatPad.MarkFor"/>), so it shows on the
    /// Amiga screens that cover the help line. The remaster's: drawn only once the pad has picked.
    /// </summary>
    public static void DrawPadMark(SpriteBatch batch, Texture2D pixel, Rectangle r)
    {
        var box = new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
        Outline(batch, pixel, box, Color.Yellow);
    }
}
