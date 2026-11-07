using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s153: a colour cycle that steps only while the map waits for input (<see cref="ColourCycle.InputWaitOnly"/>; I Amiga's
/// overworld water, Amiga <c>FORMAT.md</c> "Overworld / dungeon map graphics"). The original's input wait loop (h80
/// <c>+0x1ac</c>) calls the rotation (h77 <c>+0x178</c>) on every poll that finds no IDCMP message; the rotation takes one
/// step when h80 <c>+0xd2a</c> says <see cref="ColourCycle.StepSeconds"/> have passed since the last one and then restarts
/// that clock. So while anything else runs (a box, a requester, a step) the water stands still, and the first idle poll
/// after a pause steps at once, once (no catching up). h80 <c>+0xd2a</c> keeps only CurrentTime's seconds mod
/// <see cref="ColourCycle.ClockWrapSeconds"/> (8): crossing such a multiple makes the elapsed time negative, compared
/// unsigned, so a step comes early there. No MonoGame types: tests drive it with seconds.
/// </summary>
public sealed class InputWaitCycleClock
{
    private double? _last;

    /// <summary>The steps taken so far (<see cref="ColourCycle.SecondsAtStep"/> turns them into the cycle's phase).</summary>
    public long Steps { get; private set; }

    /// <summary>
    /// s154: the map was redrawn (h56 <c>+0x4d4</c>'s LoadRGB4 of the stored palette h69 <c>+0x74</c>) and no step has come
    /// since: the water shows its stored colours. The rotated words (h78 <c>+6</c>…<c>+0xa</c>) are kept, so the next step
    /// shows <see cref="Steps"/> + 1 as usual.
    /// </summary>
    public bool StoredShown { get; private set; }

    /// <summary>s154: the map was redrawn with its stored colours (<see cref="StoredShown"/> until the next step).</summary>
    public void Redraw() => StoredShown = true;

    /// <summary>
    /// One poll at <paramref name="seconds"/> on the machine's clock. Only a poll while <paramref name="waiting"/> (the map
    /// waits for input, nothing else runs) may step: when <paramref name="cycle"/>'s step time has passed since the last
    /// step (or its clock wrapped in between), or on the first poll.
    /// </summary>
    public void Poll(double seconds, bool waiting, ColourCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (!waiting)
            return;
        // Whole microseconds, as CurrentTime counts (0.2 s is 200000 us, h78 +2).
        if (_last is { } last && Math.Round((seconds - last) * 1e6) < Math.Round(cycle.StepSeconds * 1e6)
            && !Wrapped(last, seconds, cycle.ClockWrapSeconds))
            return;
        Steps++;
        _last = seconds;
        StoredShown = false;
    }

    private static bool Wrapped(double last, double now, double wrap) =>
        wrap > 0 && Math.Floor(now / wrap) != Math.Floor(last / wrap);
}
