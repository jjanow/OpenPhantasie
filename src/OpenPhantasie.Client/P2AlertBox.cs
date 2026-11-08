using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s158: II ST's alert boxes (AES <c>form_alert</c> after the beep <c>0xb7fa</c>; FORMAT.md 7.4) over the remaster's
/// screen: the disks' own text and button (<see cref="FieldAlertLine.Box"/>, <see cref="FieldAlerts.CueBoxes"/>), one at a
/// time in the order they came. <c>form_alert</c> waits for its button (user, s159: as on the ST): only Return / Enter
/// (the default button; EmuTOS <c>aes/gemfmlib.c</c> <c>fm_keybd</c>, the AES's own form loop) or a click on the button
/// closes the box; every other key is used up and does nothing, and nothing under the box sees the keys. No timer.
/// No MonoGame drawing (<see cref="UiOverlays.DrawStAlert"/> draws <see cref="Current"/>).
/// </summary>
public sealed class P2AlertBox
{
    private readonly Queue<StAlert> _queue = new();

    /// <summary>The box up now, or null.</summary>
    public StAlert? Current => _queue.Count > 0 ? _queue.Peek() : null;

    public bool Shown => _queue.Count > 0;

    /// <summary>Queue <paramref name="alert"/> (shown after the ones before it).</summary>
    public void Offer(StAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _queue.Enqueue(alert);
    }

    /// <summary>The key that answers the box: Return / Enter selects the default button (<c>fm_keybd</c>: RETURN, ENTER);
    /// Space and Esc do nothing in the AES form loop.</summary>
    public static bool Answers(Keys key) => key is Keys.Enter;

    /// <summary>
    /// While a box is up it owns the frame's keys: an answering key closes it (the next one shows). True when a box was
    /// up (the keys are used up); false when none was, so the keys go on.
    /// </summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        ArgumentNullException.ThrowIfNull(newKeys);
        if (!Shown)
            return false;
        if (newKeys.Any(Answers))
            _queue.Dequeue();
        return true;
    }

    /// <summary>Drop every box (a load, another session).</summary>
    public void Close() => _queue.Clear();
}
