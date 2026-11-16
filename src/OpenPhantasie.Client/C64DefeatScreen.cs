using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s223: Phantasie I / II C64's party wipe over time (<c>CEMETARY</c> / <c>CEMETARY2</c>, GRAPHICS.md 9.4), from the pack's
/// <see cref="C64DefeatLayout"/> and Core's <see cref="PartyDefeatResult"/>; Core's outcome lines (the astral line, the
/// demon's, a fate line per member) are printed where the C64 prints its own. It runs by itself; it reads no key. No
/// MonoGame types.
/// </summary>
public sealed class C64DefeatScreen
{
    /// <summary>
    /// What shows at a moment. <see cref="Temple"/>: the temple and its door (after the first line). <see cref="Ghosts"/>:
    /// slots 0…Ghosts−1 show their ghost. <see cref="DoorY"/>: the door's top (screen y). <see cref="Fates"/>: slots
    /// 0…Fates−1 show their fate (a figure in the fate's colour, or nothing).
    /// </summary>
    public readonly record struct Frame(bool Temple, int Ghosts, int DoorY, bool Face, int Fates,
        IReadOnlyList<C64EndingScreen.TextRow> Text, bool Done);

    private readonly C64DefeatLayout _layout;
    private readonly IReadOnlyList<string> _lines;
    private readonly string _defeated;

    public C64DefeatScreen(C64DefeatLayout layout, PartyDefeatResult defeat, IReadOnlyList<string> outcomeLines, string defeated)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(defeat);
        ArgumentNullException.ThrowIfNull(outcomeLines);
        _layout = layout;
        Defeat = defeat;
        _lines = outcomeLines;
        _defeated = defeated ?? "";
        var n = defeat.Fates.Count;
        TempleAt = layout.Defeated.Seconds;
        DoorAt = TempleAt + n * layout.GhostSeconds + layout.AfterGhostsSeconds;
        FaceAt = DoorAt + layout.DoorSteps * (layout.DoorStepSeconds + layout.DoorStepOverhead);
        FatesAt = FaceAt + layout.DemonSeconds;
        DoneAt = FatesAt + n * FateLength + layout.EndSeconds;
    }

    public PartyDefeatResult Defeat { get; }

    private double FateLength => _layout.FateSoundSeconds + _layout.FateCopySeconds + _layout.FateSeconds;

    /// <summary>Seconds from the start: the temple / the door starts rising / the face / the first fate / the end.</summary>
    public double TempleAt { get; }
    public double DoorAt { get; }
    public double FaceAt { get; }
    public double FatesAt { get; }
    public double DoneAt { get; }

    /// <summary>The ghosts' x (screen) per slot: floor((RowX2 − RowGap · (n − 1)) / 2) + RowGap · k (CEMETARY's SS).</summary>
    public static IReadOnlyList<int> GhostXs(int members, C64DefeatLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var first = (int)Math.Floor((layout.RowX2 - layout.RowGap * (members - 1)) / 2.0);
        return [.. Enumerable.Range(0, Math.Max(0, members)).Select(k => first + layout.RowGap * k)];
    }

    /// <summary>The fate's colour (CEMETARY: UNDEAD cyan, RESURRECTED yellow, DESTROYED dark grey).</summary>
    public int FateColour(DefeatFate fate) => fate switch
    {
        DefeatFate.Undead => _layout.UndeadColour,
        DefeatFate.Resurrected => _layout.ResurrectedColour,
        _ => _layout.DestroyedColour,
    };

    /// <summary>The screen <paramref name="t"/> seconds from the start.</summary>
    public Frame At(double t)
    {
        var n = Defeat.Fates.Count;
        var text = new List<C64EndingScreen.TextRow>();
        string? Line(int i) => i < _lines.Count ? _lines[i] : null;
        if (t < TempleAt)
        {
            text.Add(new(_layout.Defeated.Col, _layout.Defeated.Row, _defeated, _layout.Defeated.Colour));
            return new Frame(false, 0, _layout.DoorY, false, 0, text, false);
        }
        var ghosts = Math.Min(n, (int)Math.Floor((t - TempleAt) / Math.Max(1e-9, _layout.GhostSeconds) + 1e-9));
        if (_layout.GhostSeconds <= 0)
            ghosts = n;
        // CEMETARY 0x3db: FOR X = QY TO 0 STEP -1: the sprite's y := X, then the wait; so the first step keeps y.
        var step = t < DoorAt ? 0
            : Math.Min(Math.Max(0, _layout.DoorSteps - 1),
                (int)Math.Floor((t - DoorAt) / (_layout.DoorStepSeconds + _layout.DoorStepOverhead) + 1e-9));
        var doorY = _layout.DoorY - step;
        var face = t >= FaceAt;
        var fates = 0;
        if (t < FaceAt)
            text.Add(new(_layout.TextCol, _layout.TextRow, Line(0) ?? "", _layout.AstralColour));
        else if (t < FatesAt)
            text.Add(new(_layout.TextCol, _layout.TextRow, Line(1) ?? "", _layout.DemonColour));
        else
        {
            var k = Math.Min(n - 1, (int)Math.Floor((t - FatesAt) / FateLength + 1e-9));
            var into = t - FatesAt - k * FateLength;
            // CEMETARY 0x5a1-0x5a7: the rows cleared, the sound and its wait, then the line and the figure, then 2 s.
            fates = Math.Max(0, k) + (k >= 0 && into >= _layout.FateSoundSeconds + _layout.FateCopySeconds - 1e-9 ? 1 : 0);
            if (into >= _layout.FateSoundSeconds - 1e-9 && k >= 0)
                text.Add(new(_layout.TextCol, _layout.TextRow, Line(2 + k) ?? "", FateColour(Defeat.Fates[k].Fate)));
            if (t >= FatesAt + n * FateLength)
                fates = n;
        }
        return new Frame(true, ghosts, doorY, face, fates, text, t >= DoneAt);
    }
}
