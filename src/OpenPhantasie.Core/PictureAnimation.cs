namespace OpenPhantasie.Core;

/// <summary>
/// One frame of a picture animation: where it sits on the frame sheet and how long it stays (IV's frame records,
/// P4.X <c>0x45764</c>, 16 B each; <c>FORMAT.md</c> §6.10) [C]. The wait is counted in passes, see
/// <see cref="PictureAnimator"/>.
/// </summary>
public sealed record PictureAnimationFrame(int SheetX, int SheetY, int Base, int Range);

/// <summary>
/// One animated spot on a picture (IV's animation records, P4.X <c>0x46174</c>, 84 B each) [C]: its place from the
/// picture's origin, its size, and the frame ids it shows in order (wrapping to the first).
/// </summary>
public sealed record PictureAnimationRecord(int X, int Y, int Width, int Height, IReadOnlyList<int> Frames);

/// <summary>Both tables of a pack's picture animations.</summary>
public sealed record PictureAnimationTable(IReadOnlyList<PictureAnimationFrame> Frames, IReadOnlyList<PictureAnimationRecord> Records);

/// <summary>A frame change one pass made: copy <see cref="Frame"/> from the sheet onto <see cref="Record"/>'s spot.</summary>
public readonly record struct PictureFrameCopy(int Record, int Frame);

/// <summary>
/// IV's picture animation (<c>_clanim</c> <c>0x2E6C8</c>, <c>_setanim</c> <c>0x2E6F8</c>, <c>_anima</c> <c>0x2E7F8</c>;
/// <c>FORMAT.md</c> §6.10) [C]. Headless: <see cref="Pass"/> is one call of <c>_anima</c> and returns the frame copies it
/// makes; the caller draws them. P4.X calls <c>_anima</c> once per pass of its input loops with no Vsync or timer, so the
/// length of a pass in real time differs per loop (measured in XEiJ, s110: the pack's <c>pass_ms</c>); <see cref="PictureAnimationPlayer"/> runs it in real time.
/// </summary>
public sealed class PictureAnimator
{
    /// <summary><c>_anima</c> scans records 0–47.</summary>
    public const int Slots = 48;

    private readonly PictureAnimationTable _table;
    private readonly IRandomSource _rng;
    private readonly bool[] _armed = new bool[Slots];
    private readonly int[] _count = new int[Slots];
    private readonly int[] _position = new int[Slots];

    public PictureAnimator(PictureAnimationTable table, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(rng);
        _table = table;
        _rng = rng;
    }

    /// <summary>Whether record <paramref name="record"/> is armed.</summary>
    public bool IsArmed(int record) => _armed[record];

    /// <summary>The record's current position in its frame list (record +16; kept across <see cref="Clear"/> and <see cref="Arm"/>, as in P4.X).</summary>
    public int Position(int record) => _position[record];

    /// <summary>Passes left before the record's next frame change fires (it fires on the pass that finds 0).</summary>
    public int Countdown(int record) => _count[record];

    /// <summary><c>_clanim</c>: disarm every record (positions stay).</summary>
    public void Clear() => Array.Clear(_armed);

    /// <summary>
    /// <c>_setanim</c>: arm records <paramref name="first"/>..<paramref name="last"/>, each count set to
    /// <c>(rand(range) + base) &gt;&gt; 2</c> of the frame it will show next (it shows on pass count + 1). Nothing is drawn yet.
    /// </summary>
    public void Arm(int first, int last)
    {
        for (int n = first; n <= last; n++)
        {
            _count[n] = Wait(Next(n));
            _armed[n] = true;
        }
    }

    /// <summary>
    /// <c>_anima</c>: for each armed record in order, count down; when the count was already 0, show the frame at the
    /// record's position, set the count from that frame's <c>(rand(range) + base) &gt;&gt; 2</c>, and step on (wrapping).
    /// </summary>
    public IReadOnlyList<PictureFrameCopy> Pass()
    {
        List<PictureFrameCopy>? copies = null;
        for (int n = 0; n < Slots && n < _table.Records.Count; n++)
        {
            if (!_armed[n] || _count[n]-- != 0)
                continue;
            int frame = Next(n);
            (copies ??= []).Add(new PictureFrameCopy(n, frame));
            _count[n] = Wait(frame);
            _position[n]++;
            if (_position[n] >= _table.Records[n].Frames.Count)
                _position[n] = 0;
        }
        return copies ?? (IReadOnlyList<PictureFrameCopy>)[];
    }

    private int Next(int record) => _table.Records[record].Frames[_position[record]];

    private int Wait(int frame)
    {
        PictureAnimationFrame f = _table.Frames[frame];
        return (P4Dice.Rand(_rng, f.Range) + f.Base) >> 2;
    }
}

/// <summary>
/// Plays IV's picture animations in real time (T6.4, s110): one <see cref="PictureAnimator"/> for the whole game, so a
/// picture seen again carries on where it stopped (<c>_clanim</c> only disarms). <see cref="Show"/> is the picture's
/// <c>_setanim</c>; <see cref="Advance"/> turns elapsed time into <c>_anima</c> passes at the scene's measured pass length
/// (P4.X runs one pass per input-loop round, CPU-bound: <c>FORMAT.md</c> §6.10 "Timing"); <see cref="Shown"/> is what the
/// screen holds: the last frame copied onto each record's spot. Until a record's first copy the plain picture shows.
/// </summary>
public sealed class PictureAnimationPlayer
{
    /// <summary>At most this many passes per <see cref="Advance"/> (a long stall, e.g. a window drag, does not replay seconds).</summary>
    public const int MaxPassesPerAdvance = 1000;

    private readonly Dictionary<int, int> _shown = [];
    private double _carry;

    public PictureAnimationPlayer(PictureAnimationTable table, IRandomSource rng) =>
        Animator = new PictureAnimator(table, rng);

    public PictureAnimator Animator { get; }

    /// <summary>The scene key now armed (any caller-chosen name), or null.</summary>
    public string? Scene { get; private set; }

    /// <summary>The pass length in milliseconds the current scene plays at.</summary>
    public double PassMilliseconds { get; private set; }

    /// <summary>Record → the frame its spot shows now (only records copied since the picture was drawn).</summary>
    public IReadOnlyDictionary<int, int> Shown => _shown;

    /// <summary>
    /// The picture <paramref name="scene"/> is up with records <paramref name="first"/>..<paramref name="last"/>: when it is
    /// a new picture, <c>_clanim</c> + <c>_setanim</c> (the picture is drawn plain again). The same scene again changes
    /// nothing. <paramref name="passMilliseconds"/> ≤ 0 or NaN throws.
    /// </summary>
    public void Show(string scene, int first, int last, double passMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!(passMilliseconds > 0))
            throw new ArgumentOutOfRangeException(nameof(passMilliseconds));
        PassMilliseconds = passMilliseconds;
        if (scene == Scene)
            return;
        Animator.Clear();
        _shown.Clear();
        _carry = 0;
        Animator.Arm(first, last);
        Scene = scene;
    }

    /// <summary>No animated picture is up (<c>_clanim</c>).</summary>
    public void Hide()
    {
        if (Scene is null)
            return;
        Animator.Clear();
        _shown.Clear();
        _carry = 0;
        Scene = null;
    }

    /// <summary><paramref name="milliseconds"/> of real time passed: run the passes it holds; returns how many ran.</summary>
    public int Advance(double milliseconds)
    {
        if (Scene is null || !(milliseconds > 0))
            return 0;
        _carry += milliseconds;
        var passes = (int)Math.Min(Math.Floor(_carry / PassMilliseconds), MaxPassesPerAdvance);
        _carry = passes == MaxPassesPerAdvance ? 0 : _carry - passes * PassMilliseconds;
        Run(passes);
        return passes;
    }

    /// <summary>Exactly <paramref name="passes"/> <c>_anima</c> calls (a counted loop such as the Mystic's), no clock.</summary>
    public void Run(int passes)
    {
        if (Scene is null)
            return;
        for (var i = 0; i < passes; i++)
            foreach (var copy in Animator.Pass())
                _shown[copy.Record] = copy.Frame;
    }

    /// <summary>
    /// A further <c>_setanim</c> on the picture now up, without <c>_clanim</c> (the records already armed keep running), or,
    /// with <paramref name="clearFirst"/>, <c>_clanim</c> + <c>_setanim</c>. Either way what the screen holds stays: P4.X
    /// copies frames onto the picture, so a record disarmed keeps its last frame. <paramref name="passMilliseconds"/> is the
    /// new loop's pass length.
    /// </summary>
    public void Arm(int first, int last, double passMilliseconds, bool clearFirst = false)
    {
        if (Scene is null)
            throw new InvalidOperationException("No picture is up.");
        if (!(passMilliseconds > 0))
            throw new ArgumentOutOfRangeException(nameof(passMilliseconds));
        if (clearFirst)
            Animator.Clear();
        Animator.Arm(first, last);
        PassMilliseconds = passMilliseconds;
        _carry = 0;
    }
}
