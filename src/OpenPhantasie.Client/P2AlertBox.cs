using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s158: II ST's alert boxes (AES <c>form_alert</c> after the beep <c>0xb7fa</c>; FORMAT.md 7.4) over the remaster's
/// screen: the disks' own text and button (<see cref="FieldAlertLine.Box"/>, <see cref="FieldAlerts.CueBoxes"/>), one at a
/// time in the order they came. <c>form_alert</c> waits for its button: Return (the default button), a click on the
/// button, and here also Space / Esc close the box and are used up; nothing under it sees the keys. No MonoGame drawing
/// (<see cref="UiOverlays.DrawStAlert"/> draws <see cref="Current"/>).
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

    /// <summary>The keys that answer the box (its one button).</summary>
    public static bool Answers(Keys key) => key is Keys.Enter or Keys.Space or Keys.Escape;

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
