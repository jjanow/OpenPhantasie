namespace OpenPhantasie.Client;

/// <summary>
/// s165: II ST's timed box in the field (FORMAT.md 7.3a / 7.4): field Resurrection's "%s IS RESURRECTED" goes through
/// <c>0xa18c</c> → <c>0xa124</c> (OUT.RSC tree 40, the end-of-fight one-line box at (12, 75)), shown by
/// <c>0x16dce</c> and held by <c>0x16cbe</c>(Speed + 1000): <c>evnt_multi</c> keyboard | button | timer, so the timer,
/// any key or a click ends it (<c>0x16d56</c> first drains what was queued). Then <c>0xa18c</c> redraws the party view
/// (<c>0x17210</c>); <c>0x92c8</c> writes nothing to the message line. While the box is up it takes every key and click
/// (the field loop is not running). No MonoGame types: tests drive it with times.
/// </summary>
public sealed class P2FieldBox
{
    private double _until;

    /// <summary>The box's rows (one: tree 40), or null when no box is up.</summary>
    public IReadOnlyList<string>? Rows { get; private set; }

    /// <summary>The game mode the box was opened in (its palette), lower case.</summary>
    public string Mode { get; private set; } = "";

    /// <summary>Opens the box on <paramref name="text"/> at <paramref name="now"/> seconds for <paramref name="holdMs"/>.</summary>
    public void Open(string text, string mode, double now, double holdMs)
    {
        ArgumentNullException.ThrowIfNull(text);
        Rows = [text.Trim()];
        Mode = mode ?? "";
        _until = now + Math.Max(0, holdMs) / 1000.0;
    }

    /// <summary>
    /// One frame at <paramref name="now"/>: the hold runs out, or <paramref name="pressed"/> (a new key or a click) ends it.
    /// True while the box owned the frame (it was up when the frame began), so the input goes no further.
    /// </summary>
    public bool Update(double now, bool pressed)
    {
        if (Rows is null)
            return false;
        if (pressed || now >= _until)
            Close();
        return true;
    }

    public void Close() => Rows = null;
}
