namespace OpenPhantasie.Client;

/// <summary>
/// The town screen's message band clock (s98 band, s102 fix): a message shows for a while after it changes, and again
/// after <see cref="Restart"/> — a town action that ends in the same text as before (a second "not enough gold") shows it
/// again. Passing the current message through (help refreshes, every frame) does not restart it.
/// </summary>
public sealed class MessageBandClock
{
    private string? _text;
    private int _stamp;
    private int _seenStamp;
    private double _since = double.NegativeInfinity;

    /// <summary>Show the current message again from now (an action re-sent it), even when its text has not changed.</summary>
    public void Restart() => _stamp++;

    /// <summary>True while <paramref name="message"/> is within <paramref name="seconds"/> of its last change or restart.</summary>
    public bool Shows(string message, double now, double seconds)
    {
        if (message != _text || _stamp != _seenStamp)
        {
            _text = message;
            _seenStamp = _stamp;
            _since = now;
        }
        return now - _since < seconds;
    }
}
