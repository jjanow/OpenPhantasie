using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s154: Phantasie I DOS's party-defeat lines (OV1 <c>0x24c</c>, <c>endgame.md</c> 4 "Timing"): each line holds
/// <see cref="DosDefeatTiming.LineSeconds"/> (500 hundredths, COM <c>0x3bf8</c>) and, when
/// <see cref="DosDefeatTiming.KeyEndsLine"/>, a key ends that line's wait early (the next line starts then). One clock per
/// defeat prompt: <paramref name="steps"/> lines from <paramref name="start"/> (seconds). No MonoGame types.
/// </summary>
public sealed class P1DosDefeatClock(SessionPromptKind kind, int steps, DosDefeatTiming timing, double start)
{
    private int _index;
    private double _since = start;

    /// <summary>The prompt this clock times.</summary>
    public SessionPromptKind Kind { get; } = kind;

    /// <summary>The line up at <paramref name="now"/> (0-based; <c>steps</c> once all are over).</summary>
    public int Index(double now)
    {
        Advance(now);
        return _index;
    }

    /// <summary>How many of <paramref name="count"/> lines show at <paramref name="now"/> (they add up, none is dropped).</summary>
    public int LinesShown(double now, int count) => Math.Clamp(Index(now) + 1, 1, Math.Max(1, count));

    /// <summary>True once the last line's wait is over.</summary>
    public bool Done(double now) => Index(now) >= steps;

    /// <summary>A key at <paramref name="now"/>: ends the current line's wait when DOS lets it.</summary>
    public void Key(double now)
    {
        Advance(now);
        if (!timing.KeyEndsLine || _index >= steps)
            return;
        _index++;
        _since = now;
    }

    private void Advance(double now)
    {
        while (_index < steps && now - _since >= timing.LineSeconds)
        {
            _since += timing.LineSeconds;
            _index++;
        }
    }
}
