using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client;

/// <summary>
/// Phantasie I Amiga's party-defeat screens over time (hunk 32 <c>+0</c> / <c>+0x55c</c>, s153; the pack's
/// <see cref="DefeatScreenLayout"/>): they run by themselves, each text waits its polls or a key or click, the sounds hold
/// the screen and drain the input port. Built from Core's <see cref="PartyDefeatResult"/> (its log: the three scene lines,
/// then one fate line per member). No MonoGame types: tests drive it with seconds.
/// </summary>
public sealed class P1DefeatScreen
{
    /// <summary>What shows at a moment.</summary>
    /// <param name="Black">The screen is black (<c>decor.pic</c> loading).</param>
    /// <param name="Text">The line on the text row (it stays until the next prints), or null.</param>
    /// <param name="Ghosts">Ghosts blitted so far (from slot 0).</param>
    /// <param name="DoorHeight">Rows of the door left (90 closed, 0 gone).</param>
    /// <param name="Face">The Great Demon's face shows behind the open door.</param>
    /// <param name="Fates">Members whose fate shows (from slot 0).</param>
    /// <param name="Done">The screens are over (the game goes on).</param>
    public readonly record struct Frame(bool Black, string? Text, int Ghosts, int DoorHeight, bool Face, int Fates, bool Done)
    {
        /// <summary>The door is sinking (the Amiga's raw tone sounds then).</summary>
        public bool DoorSinking { get; init; }

        /// <summary>s154: the 5th defeat's sign shows (h32 <c>+0x64c</c>), until the face covers it.</summary>
        public bool Sign { get; init; }
    }

    private enum StepKind { Black, Line, Ghosts, Sound, Door, Delay, Face, End, Sign }

    private readonly record struct Step(StepKind Kind, int Index, double Seconds, bool Keyable, int? Sound = null)
    {
        public SoundHold? Hold { get; init; }
        public bool Drains => Hold is not null;
    }

    private readonly DefeatScreenLayout _layout;
    private readonly List<Step> _steps = [];
    private readonly Dictionary<int, double> _cuts = [];

    /// <param name="hold">How sound n holds the screen (null: the Sound switch is off, no waits; the drains stay).</param>
    /// <param name="defeatsThisRun">The defeats counted so far, this one included (s154: the sign on the layout's
    /// <see cref="DefeatScreenLayout.SignDefeat"/>th); 0 = not counted.</param>
    public P1DefeatScreen(DefeatScreenLayout layout, PartyDefeatResult defeat, Func<int, SoundHold>? hold = null,
        int defeatsThisRun = 0)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(defeat);
        _layout = layout;
        Defeat = defeat;
        Lines = defeat.Log;
        var poll = layout.PollSeconds;
        var line = layout.LinePolls * poll;
        var blit = layout.BlitVbl / layout.VblHz;
        void Sound(int n)
        {
            var h = hold?.Invoke(n) ?? SoundHold.None;
            _steps.Add(new(StepKind.Sound, n, h.Seconds, false, hold is null ? null : n) { Hold = h });
        }
        _steps.Add(new(StepKind.Black, 0, layout.PictureRead, false));
        _steps.Add(new(StepKind.Line, 0, line, true));
        _steps.Add(new(StepKind.Ghosts, 0, Members * blit, false));
        Sound(layout.GhostSound);
        _steps.Add(new(StepKind.Line, 1, line, true));
        _steps.Add(new(StepKind.Delay, 0, layout.DoorLeadSeconds, false)); // the raw tone starts
        _steps.Add(new(StepKind.Door, 0, layout.DoorSeconds, false));
        if (layout.SignDefeat is int signAt && defeatsThisRun == signAt) // s154: h32 +0x63c..+0x67e, then the Delay
            _steps.Add(new(StepKind.Sign, 0, layout.SignPolls * poll, layout.SignPolls > 0));
        _steps.Add(new(StepKind.Delay, 1, layout.DoorTailSeconds + layout.AfterDoorTicks / 50.0, false)); // tone off, Delay
        Sound(layout.DemonSound);
        _steps.Add(new(StepKind.Face, 0, blit, false));
        _steps.Add(new(StepKind.Line, 2, line, true));
        for (var i = 0; i < defeat.Fates.Count; i++)
        {
            Sound(layout.FateSound);
            _steps.Add(new(StepKind.Line, 3 + i, line, true));
        }
        _steps.Add(new(StepKind.End, 0, layout.EndPolls * poll, true));
    }

    public PartyDefeatResult Defeat { get; }

    /// <summary>The scene's lines: Core's defeat log (three scene lines, then a fate line per member).</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Slots in the row: every judged member.</summary>
    public int Members => Defeat.Fates.Count;

    private double Start(int i) => i == 0 ? 0 : End(i - 1);

    private double End(int i) => Start(i) + (_cuts.TryGetValue(i, out var cut) ? cut : _steps[i].Seconds);

    /// <summary>Seconds from the start when the screens are over.</summary>
    public double DoneAt => End(_steps.Count - 1);

    /// <summary>Seconds from the start when the door starts to sink.</summary>
    public double DoorAt => Start(_steps.FindIndex(s => s.Kind == StepKind.Door));

    /// <summary>
    /// s154: when the raw tone (h89 <c>+0x7ca</c>) starts and stops (<c>+0x7f4</c>): from the end of line 2's wait (the
    /// door's lead) to the door's last step.
    /// </summary>
    public (double Start, double End) Tone
    {
        get
        {
            var door = _steps.FindIndex(s => s.Kind == StepKind.Door);
            return (Start(door - 1), End(door));
        }
    }

    /// <summary>When each sound starts and its hold ends, in order (empty with the Sound switch off).</summary>
    public IReadOnlyList<(double At, int Sound, double End)> SoundStarts()
    {
        var list = new List<(double, int, double)>();
        for (var i = 0; i < _steps.Count; i++)
            if (_steps[i].Sound is int n)
                list.Add((Start(i), n, End(i)));
        return list;
    }

    /// <summary>A key (or the mouse button) pressed at <paramref name="t"/>: one event in I's input port.</summary>
    public void Key(double t) => Event(t);

    /// <summary>A key (or the mouse button) released at <paramref name="t"/>: another event.</summary>
    public void Release(double t) => Event(t);

    private int? Current(double t)
    {
        for (var i = 0; i < _steps.Count; i++)
        {
            if (t < Start(i))
                return null;
            if (t < End(i))
                return i;
        }
        return null;
    }

    // As the victory screen (VictoryScreen.Event): a line or the last wait ends at once; a sound at its step's end; any
    // other moment queues the event until a sound drains it or the next line takes it at its first poll.
    private void Event(double t)
    {
        if (Current(t) is not int i)
            return;
        var step = _steps[i];
        if (step.Keyable && !_cuts.ContainsKey(i))
        {
            _cuts[i] = t - Start(i);
            return;
        }
        if (step.Hold is { } hold && !_cuts.ContainsKey(i))
        {
            _cuts[i] = Math.Min(step.Seconds, hold.CutAt(t - Start(i)));
            return;
        }
        for (var k = i + 1; k < _steps.Count; k++)
        {
            if (_steps[k].Drains)
                return;
            if (_steps[k].Keyable && !_cuts.ContainsKey(k))
            {
                _cuts[k] = Math.Min(_steps[k].Seconds, _layout.PollSeconds);
                return;
            }
        }
    }

    /// <summary>The screens <paramref name="t"/> seconds from the start.</summary>
    public Frame At(double t)
    {
        bool black = true, face = false, sinking = false, done = true, sign = false;
        string? text = null;
        int ghosts = 0, fates = 0, door = DefeatScene.DoorHeightClosed;
        for (var i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            var start = Start(i);
            if (t < start)
            {
                done = false;
                break;
            }
            var into = t - start;
            var current = t < End(i);
            switch (step.Kind)
            {
                case StepKind.Black:
                    black = current;
                    break;
                case StepKind.Line:
                    text = step.Index < Lines.Count ? Lines[step.Index] : null;
                    if (step.Index >= 3)
                        fates = step.Index - 2;
                    break;
                case StepKind.Ghosts:
                    ghosts = current && _layout.BlitVbl > 0
                        ? Math.Min(Members, (int)Math.Floor(into * _layout.VblHz / _layout.BlitVbl + 1e-9) + 1)
                        : Members;
                    break;
                case StepKind.Door:
                    sinking = current;
                    door = current ? DoorHeight(into) : 0;
                    break;
                case StepKind.Face:
                    face = true;
                    sign = false; // the face's blit covers the sign
                    break;
                case StepKind.Sign:
                    sign = true;
                    break;
            }
            if (current)
            {
                done = false;
                break;
            }
        }
        return new Frame(black, text, ghosts, door, face, fates, done) { DoorSinking = sinking, Sign = sign };
    }

    /// <summary>Door rows left <paramref name="seconds"/> into its sinking: 90 steps over the layout's door time.</summary>
    private int DoorHeight(double seconds)
    {
        if (_layout.DoorSeconds <= 0)
            return 0;
        var steps = (int)Math.Floor(seconds / _layout.DoorSeconds * DefeatScene.Steps + 1e-9);
        var h = DefeatScene.DoorHeightClosed - steps;
        return h <= 1 ? 0 : h;
    }

    /// <summary>The row this frame: ghosts in the first <see cref="Frame.Ghosts"/> slots, then each judged member's fate
    /// (a destroyed member's slot cleared, the others' race portraits, UNDEAD's 15).</summary>
    public IReadOnlyList<DefeatPortrait> Row(Frame frame)
    {
        var row = new List<DefeatPortrait>();
        for (var i = 0; i < Members; i++)
        {
            var (member, fate) = Defeat.Fates[i];
            row.Add(i < frame.Fates
                ? fate == DefeatFate.Destroyed
                    ? new DefeatPortrait(DefeatPortraitKind.Cleared, 0)
                    : new DefeatPortrait(DefeatPortraitKind.Race, (int)member.Race)
                : i < frame.Ghosts
                    ? new DefeatPortrait(DefeatPortraitKind.Ghost, (int)member.Race)
                    : new DefeatPortrait(DefeatPortraitKind.Cleared, 0));
        }
        return row;
    }
}
