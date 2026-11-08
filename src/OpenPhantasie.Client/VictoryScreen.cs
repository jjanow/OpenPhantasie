using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client;

/// <summary>
/// The victory screen over time, built from the pack's <see cref="EndingSceneLayout"/> and Core's
/// <see cref="VictoryScene"/>. No MonoGame types: tests drive it with seconds.
/// Phantasie I Amiga (hunk 32 <c>+0x6d0</c>, s127 static):
/// <list type="number">
/// <item>Fade-in: pen k of the picture's table is set at <c>k · FadeVbl</c> VBLs (k = 0…15), then one more wait.</item>
/// <item><c>Delay(HoldTicks)</c> (1/50 s ticks).</item>
/// <item>The ghosts in slots 0…n−1 (n = members granted DIVINE I), then Zeus' raised arm.</item>
/// <item>Per granted member in slot order: its portrait over its slot, then <c>Delay(GrantTicks)</c>.</item>
/// <item>The speech, one line at a time; each stays <c>LineTicks</c> polls (<c>LinePollSeconds</c> each, I: 21.87 ms) or
/// until a key (<see cref="Key"/>).</item>
/// <item>Fade-out: pen 15 − k turns black at <c>k · FadeVbl</c> VBLs, then one more wait; then <see cref="Frame.Done"/>.</item>
/// </list>
/// s130 (live capture, <c>capture_s130_victory.md</c>): the layout's disk waits (<see cref="EndingDiskWaits"/>:
/// <c>final.pic</c> read after the first sound, <c>guild.dat</c> written among the sounds after the speech, the town
/// reloaded on black after the fade-out); each sound holds the screen per step (<see cref="SoundHold"/>); with
/// <see cref="EndingSceneLayout.QueuedKeys"/> presses and releases go through I's input port (<see cref="Release"/>).
/// s128: with <see cref="VictorySoundTimes"/> the sounds h89 <c>+0xee</c> plays hold the screen for their length (the
/// player <c>+0x304</c> blocks; Amiga FORMAT.md "Sound effects"): one before the black and one on black before the fade-in,
/// one between the ghosts and the arm, one after each portrait (before its <c>Delay</c>), and some between the last line
/// and the fade-out. <see cref="SoundStarts"/> lists when each starts. Without them the timeline has no sound waits
/// (also with the Sound switch off, user s129: <c>Game1</c> passes none then).
/// <para>Phantasie II ST (<c>0x12f2a</c>, s128; <c>endgame.md</c> §7.1) adds the layout's optional steps: the intro page
/// (until a key) before the fade-in; the header lines (each <c>LineTicks</c> or a key) before the hold; per presented
/// member its title (a key-able line) with its idle portrait on the stage, then its other pose and the crowd cheering
/// (<see cref="EndingCrowd"/>), then its portrait in the row; the stage cleared before the speech; the closing page (until
/// a key) before the fade-out. s135: with <c>stSounds</c> the crowd's <see cref="EndingCrowd.Sound"/> (II: 32,
/// <c>0xb80e(32)</c>) starts with each cheer, and a page's <see cref="EndingPage.Sound"/> (the OK form's click,
/// <c>0xb7e6</c>) when OK closes it; neither holds the screen (Dosound returns at once): their
/// <see cref="SoundStarts"/> end is ∞, so it plays out unless the next sound cuts it. s163: with <c>stSoundSeconds</c> the
/// next sound 32 waits for the last to end (<c>0xb8f2</c> at the ending's Speed 20), the other pose on the stage, which
/// only bites when keys cut the title lines short.</para>
/// <para>All of it is one list of steps; each step lasts its time, or until a key when it is a line or a page.</para>
/// </summary>
public sealed class VictoryScreen
{
    /// <summary>The Amiga's <c>Delay</c> tick (dos.library: 1/50 s on every machine).</summary>
    public const double TickSeconds = 1 / 50.0;

    /// <summary>What shows at a moment. <see cref="LitPens"/>: pens 0…LitPens−1 have their colour (fade-in);
    /// <see cref="DarkPens"/>: pens 16−DarkPens…15 are black again (fade-out). <see cref="Line"/>: the speech line on
    /// screen (it stays through the fade-out), or −1.</summary>
    public readonly record struct Frame(int LitPens, int DarkPens, bool Ghosts, int Granted, int Line, bool Done)
    {
        /// <summary>Zeus' raised arm shows (after the ghosts and the sound between them).</summary>
        public bool Arm { get; init; }

        /// <summary>How many of the ghosts are drawn yet (I Amiga blits one a frame, s153 live); all once
        /// <see cref="Ghosts"/> holds and the blits are over.</summary>
        public int GhostCount { get; init; } = int.MaxValue;

        /// <summary>Pen <paramref name="pen"/> shows its colour now.</summary>
        public bool Shows(int pen) => pen < LitPens && pen < 16 - DarkPens;

        /// <summary>The text on the bottom line now (a header, title or speech line; it stays until the next), or null.</summary>
        public string? Text { get; init; }

        /// <summary>The page on screen instead of the picture.</summary>
        public EndingPageKind Page { get; init; }

        /// <summary>The slot index whose portrait stands on the stage, or −1 (none, or cleared: <see cref="StageCleared"/>).</summary>
        public int Stage { get; init; } = -1;

        /// <summary>The stage portrait shows its other pose (II: <c>PARTY2</c>).</summary>
        public bool StagePose { get; init; }

        /// <summary>The stage spot has been cleared to pen 0 (II, after the presentations).</summary>
        public bool StageCleared { get; init; }

        /// <summary>The crowd frame drawn over the picture, or −1 (the picture's own pixels).</summary>
        public int Crowd { get; init; } = -1;
    }

    /// <summary>A text page that replaces the picture (II ST).</summary>
    public enum EndingPageKind { None, Intro, Closing }

    private enum StepKind
    {
        IntroPage, Sound, Disk, FadeIn, Header, Hold, Ghosts, ArmSound, ArmBlit, Title, CheerWait, Cheer, PortraitBlit, Grant,
        GrantDelay, Speech, ClosingPage, FadeOut,
    }

    /// <summary>One step of the screen: its length (∞ = until a key), whether a key ends it at once (a line or page), the
    /// sound that starts with it (or null), and for a sound wait how a key ends it (<see cref="Hold"/>; I Amiga's player
    /// <c>h89 +0x304</c> drains the input port first, <see cref="Drains"/>).</summary>
    private readonly record struct Step(StepKind Kind, int Index, double Seconds, bool Keyable, int? Sound = null)
    {
        public SoundHold? Hold { get; init; }

        /// <summary>The sound runs on past the step (II's Dosound): it never holds the screen.</summary>
        public bool Overlaps { get; init; }

        public bool Drains => Hold is not null;
    }

    private readonly EndingSceneLayout _layout;
    private readonly int _grants;
    private readonly List<Step> _steps = [];
    private readonly Dictionary<int, double> _cuts = []; // step index → its length after a key ended it
    private readonly double _crowdSoundSeconds; // II: sound 32's length (0 = no wait modelled)

    public VictoryScreen(EndingSceneLayout layout, VictoryScene scene, VictorySoundTimes? sounds = null,
        bool stSounds = false, Func<int, double>? stSoundSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        _layout = layout;
        Scene = scene;
        // The same speech in this version's line breaks; a scene with other lines (tests) keeps its own.
        SpeechLines = layout.SpeechKeys is { } keys && keys.Count == scene.SpeechLines.Count
            ? [.. keys.Select(GameText.S)]
            : scene.SpeechLines;
        _grants = scene.Slots.Count(s => s.Granted);
        _crowdSoundSeconds = stSounds && layout.Crowd?.Sound is int crowdSound && stSoundSeconds is not null
            ? Math.Max(0, stSoundSeconds(crowdSound))
            : 0;
        var fx = sounds ?? VictorySoundTimes.None;
        var n = fx.Numbers; // null: no sounds and no sound waits (with the layout's sounds the drains stay, zero long)
        var waits = layout.Sounds is not null || n is not null;
        var disk = layout.Disk;
        var tick = 1 / layout.TickHz;
        var line = layout.LineTicks * (layout.LinePollSeconds ?? tick);
        void Sound(StepKind kind, int index, SoundHold hold, int? number) =>
            _steps.Add(new(kind, index, n is null ? 0 : hold.Seconds, false, n is null ? null : number)
            {
                Hold = layout.QueuedKeys && n is not null ? hold : layout.QueuedKeys ? SoundHold.None : null,
            });
        if (layout.IntroPage is not null && scene.IntroPage.Count > 0)
        {
            _steps.Add(new(StepKind.IntroPage, 0, double.PositiveInfinity, true));
            if (stSounds && layout.IntroPage.Sound is int click)
                _steps.Add(new(StepKind.Sound, -1, 0, false, click) { Overlaps = true });
        }
        if (waits)
        {
            Sound(StepKind.Sound, 0, fx.BeforeBlack, n?.BeforeBlack);
            if (disk is not null)
                _steps.Add(new(StepKind.Disk, 0, disk.PictureRead, false));
            Sound(StepKind.Sound, 1, fx.OnBlack, n?.OnBlack);
        }
        _steps.Add(new(StepKind.FadeIn, 0, FadeSeconds, false));
        for (var i = 0; i < scene.HeaderLines.Count; i++)
            _steps.Add(new(StepKind.Header, i, line, true));
        _steps.Add(new(StepKind.Hold, 0, layout.HoldTicks * tick, false));
        // s153 live: h77 +0xcfe waits a WaitTOF before each blit, so the ghosts land one a frame, the arm one frame after
        // sound 1, and each portrait one frame before its sound 21.
        var blit = layout.BlitVbl / layout.VblHz;
        if (blit > 0)
            _steps.Add(new(StepKind.Ghosts, 0, _grants * blit, false));
        if (waits)
            Sound(StepKind.ArmSound, 0, fx.BeforeArm, n?.BeforeArm);
        else
            _steps.Add(new(StepKind.ArmSound, 0, 0, false));
        if (blit > 0)
            _steps.Add(new(StepKind.ArmBlit, 0, blit, false));
        for (var k = 0; k < _grants; k++)
        {
            if (k < scene.Titles.Count)
                _steps.Add(new(StepKind.Title, k, line, true));
            // s163: the next 32 waits for the last (0xb8f2 at the ceremony's Speed 20), after the other pose: it only bites
            // when keys cut the title lines short (CheerWait's length is worked out in End).
            if (layout.Crowd is not null && k > 0 && _crowdSoundSeconds > 0)
                _steps.Add(new(StepKind.CheerWait, k, 0, false));
            if (layout.Crowd is { } crowd)
                _steps.Add(new(StepKind.Cheer, k, crowd.Flashes * crowd.FlashTicks * tick, false,
                    stSounds ? crowd.Sound : null) { Overlaps = true });
            // The portrait, its sound (h89 +0xee(21), blocking), then Delay(GrantTicks).
            if (blit > 0)
                _steps.Add(new(StepKind.PortraitBlit, k, blit, false));
            if (waits)
                Sound(StepKind.Grant, k, fx.AfterPortrait, n?.AfterPortrait);
            else
                _steps.Add(new(StepKind.Grant, k, 0, false));
            _steps.Add(new(StepKind.GrantDelay, k, layout.GrantTicks * tick, false));
        }
        for (var i = 0; i < SpeechLines.Count; i++)
            _steps.Add(new(StepKind.Speech, i, line, true));
        if (waits)
        {
            var after = layout.Sounds?.AfterSpeech ?? [.. fx.AfterSpeech.Select(a => a.Sound)];
            for (var i = 0; i <= after.Count; i++)
            {
                if (disk is not null && i == Math.Min(disk.SaveAfterSounds, after.Count))
                    _steps.Add(new(StepKind.Disk, 1, disk.Save, false));
                if (i < after.Count)
                    Sound(StepKind.Sound, 2 + i, i < fx.AfterSpeech.Count ? fx.AfterSpeech[i].Hold : SoundHold.None, after[i]);
            }
        }
        if (layout.ClosingPage is not null && scene.ClosingPage.Count > 0)
        {
            _steps.Add(new(StepKind.ClosingPage, 0, double.PositiveInfinity, true));
            if (stSounds && layout.ClosingPage.Sound is int click)
                _steps.Add(new(StepKind.Sound, -1, 0, false, click) { Overlaps = true });
        }
        _steps.Add(new(StepKind.FadeOut, 0, FadeSeconds, false));
        if (disk is { TownLoad: > 0 })
            _steps.Add(new(StepKind.Disk, 2, disk.TownLoad, false));
    }

    public VictoryScene Scene { get; }

    /// <summary>The speech as this version prints it (the layout's <see cref="EndingSceneLayout.SpeechKeys"/> when they
    /// are as many as Core's lines, else Core's).</summary>
    public IReadOnlyList<string> SpeechLines { get; }

    private double FadeSeconds => 16.0 * _layout.FadeVbl / _layout.VblHz;

    private double Start(int i) => i == 0 ? 0 : End(i - 1);

    private double End(int i) => Start(i) + (_cuts.TryGetValue(i, out var cut) ? cut : Length(i));

    // A step's own length; II's CheerWait lasts until the last cheer's sound 32 has ended (none when it already has).
    private double Length(int i)
    {
        if (_steps[i].Kind != StepKind.CheerWait)
            return _steps[i].Seconds;
        var last = _steps.FindLastIndex(i, s => s.Kind == StepKind.Cheer);
        return last < 0 ? 0 : Math.Max(0, Start(last) + _crowdSoundSeconds - Start(i));
    }

    private int First(StepKind kind) => _steps.FindIndex(s => s.Kind == kind);

    /// <summary>Seconds from the start when the fade-in begins (after the intro page and the sounds before it).</summary>
    public double FadeInAt => Start(First(StepKind.FadeIn));

    /// <summary>Seconds from the start when the ghosts appear (the presentations begin).</summary>
    public double GhostsAt => End(First(StepKind.Hold));

    /// <summary>Seconds from the start when Zeus' arm appears and the first portrait with it.</summary>
    public double ArmAt => End(First(StepKind.ArmSound));

    /// <summary>Seconds from the start when the speech's first line prints.</summary>
    public double SpeechAt => Start(_steps.FindIndex(First(StepKind.ArmSound),
        s => s.Kind is StepKind.Speech or StepKind.Sound or StepKind.ClosingPage or StepKind.FadeOut));

    /// <summary>Seconds from the start when the last line's wait is over (the sounds after the speech start).</summary>
    public double SpeechEnd => _steps.FindLastIndex(s => s.Kind == StepKind.Speech) is var last and >= 0 ? End(last) : SpeechAt;

    /// <summary>Seconds from the start when the fade-out begins (the last line, the sounds after it and the closing page are over).</summary>
    public double FadeOutAt => Start(First(StepKind.FadeOut));

    /// <summary>Seconds from the start when the screen is over (after the fade-out and the town load).</summary>
    public double DoneAt => End(_steps.Count - 1);

    /// <summary>
    /// When each sound starts and when its wait ends (seconds from the start, sound number), in order; empty without sound
    /// times. A key moves the later ones, so read it again after <see cref="Key"/> / <see cref="Release"/>.
    /// </summary>
    /// <summary>
    /// s156: whether a key or click cut the <paramref name="index"/>th sound of <see cref="SoundStarts"/> short (its end is
    /// the cut's, not its own length): only then does the front end stop it at that end. An uncut sound plays out, so a slow
    /// first frame (the audio starting late against the screen's clock) no longer clips sound 22.
    /// </summary>
    public bool SoundCut(int index)
    {
        var n = 0;
        for (var i = 0; i < _steps.Count; i++)
            if (_steps[i].Sound is not null && n++ == index)
                return _cuts.TryGetValue(i, out var cut) && cut < _steps[i].Seconds;
        return false;
    }

    public IReadOnlyList<(double At, int Sound, double End)> SoundStarts()
    {
        var list = new List<(double, int, double)>();
        for (var i = 0; i < _steps.Count; i++)
            if (_steps[i].Sound is int sound)
                list.Add((Start(i), sound, _steps[i].Overlaps ? double.PositiveInfinity : End(i)));
        return list;
    }

    /// <summary>
    /// A key pressed at <paramref name="t"/> seconds from the start. II (<c>evnt_multi</c> / the OK form): it ends the line
    /// or page waiting then; at any other time the ST reads no key. I Amiga (<see cref="EndingSceneLayout.QueuedKeys"/>):
    /// an input event, see <see cref="Release"/>.
    /// </summary>
    public void Key(double t)
    {
        if (_layout.QueuedKeys)
            Event(t);
        else if (Current(t) is int i && _steps[i].Keyable)
            _cuts[i] = t - Start(i);
    }

    /// <summary>
    /// A key released at <paramref name="t"/> (I Amiga only; II ignores it). I's input port gets the press and the release
    /// (s130 live `[C]`): during a sound the event ends it at the current step (<c>h89 +0x304</c>); during a line it ends
    /// the line (<c>h62 +0x576</c>); otherwise it waits in the port, where the next sound drains it or the next line takes
    /// it at that line's first poll (a key in the last delay ends line 0 with its press and line 1 with its release).
    /// </summary>
    public void Release(double t)
    {
        if (_layout.QueuedKeys)
            Event(t);
    }

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

    private void Event(double t)
    {
        if (Current(t) is not int i)
            return;
        var step = _steps[i];
        if (step.Keyable)
        {
            _cuts[i] = t - Start(i);
            return;
        }
        if (step.Hold is { } hold && !_cuts.ContainsKey(i))
        {
            _cuts[i] = Math.Min(step.Seconds, hold.CutAt(t - Start(i)));
            return;
        }
        // A sound already ended by an earlier event reads no more (h89 +0x304 takes one message a step and returns at the
        // cut, s153 static): this one stays in the port as below.
        // Queued: the next sound drains it, or the next line not yet ended by the queue takes it at its first poll.
        for (var k = i + 1; k < _steps.Count; k++)
        {
            if (_steps[k].Drains)
                return;
            if (_steps[k].Keyable && !_cuts.ContainsKey(k))
            {
                _cuts[k] = Math.Min(_steps[k].Seconds, _layout.LinePollSeconds ?? 0);
                return;
            }
        }
    }

    /// <summary>The screen <paramref name="t"/> seconds from the start.</summary>
    public Frame At(double t)
    {
        var vbl = _layout.VblHz / _layout.FadeVbl; // fade steps a second
        int lit = 0, dark = 0, granted = 0, line = -1, stage = -1, crowd = -1, ghosts = int.MaxValue;
        bool pose = false, cleared = false, done = true;
        string? text = null;
        var page = EndingPageKind.None;
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
                case StepKind.IntroPage:
                    page = current ? EndingPageKind.Intro : EndingPageKind.None;
                    break;
                case StepKind.FadeIn:
                    lit = Math.Min(16, (int)Math.Floor(into * vbl + 1e-9) + 1);
                    break;
                case StepKind.Header:
                    text = Scene.HeaderLines[step.Index];
                    break;
                case StepKind.Title:
                    text = Scene.Titles[step.Index];
                    (stage, pose) = (step.Index, false);
                    break;
                case StepKind.CheerWait: // the other pose shows; the crowd keeps its last frame
                    (stage, pose) = (step.Index, true);
                    break;
                case StepKind.Cheer:
                    (stage, pose) = (step.Index, true);
                    var c = _layout.Crowd!;
                    crowd = current
                        ? (int)Math.Floor(into * _layout.TickHz / c.FlashTicks + 1e-9) % 2
                        : c.Frames.Count - 1;
                    break;
                case StepKind.Ghosts:
                    ghosts = current ? (int)Math.Floor(into * _layout.VblHz / _layout.BlitVbl + 1e-9) + 1 : int.MaxValue;
                    break;
                case StepKind.PortraitBlit:
                case StepKind.Grant:
                    granted = step.Index + 1;
                    break;
                case StepKind.Speech:
                    line = step.Index;
                    text = SpeechLines[step.Index];
                    if (stage >= 0)
                        (stage, cleared) = (-1, true);
                    break;
                case StepKind.ClosingPage:
                    (page, text) = (EndingPageKind.Closing, null);
                    break;
                case StepKind.FadeOut:
                    dark = Math.Min(16, (int)Math.Floor(into * vbl + 1e-9) + 1);
                    if (current)
                        done = false;
                    break;
            }
            if (current && step.Kind != StepKind.FadeOut)
            {
                done = false;
                break;
            }
        }
        var art = _layout.Ghost is not null || _layout.Arm is not null;
        if (_layout.StageX is null)
            (stage, cleared) = (-1, false);
        return new Frame(lit, dark, art && t >= GhostsAt, granted, line, done)
        {
            Arm = art && t >= ArmAt, Text = text, Page = page, Stage = stage, StagePose = pose, StageCleared = cleared,
            Crowd = crowd, GhostCount = ghosts,
        };
    }

    /// <summary>
    /// The x of each slot's 32-wide box on the row (h32 <c>+0x36e</c>(1); II <c>0x12a8a(1)</c>): n = members granted, gap
    /// <c>(320 − size·n) / (n + 1)</c> (h53 <c>+0</c>, truncating); a granted member takes the next place, any other slot
    /// the place the next granted member will take.
    /// </summary>
    public static IReadOnlyList<int> SlotXs(IReadOnlyList<VictorySlot> slots, int size = 32)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var n = slots.Count(s => s.Granted);
        var gap = (320 - size * n) / (n + 1);
        var xs = new List<int>();
        var x = gap;
        foreach (var slot in slots)
        {
            xs.Add(x);
            if (slot.Granted)
                x += gap + size;
        }
        return xs;
    }

    /// <summary>
    /// The slots drawn now: a ghost in slots 0…n−1 (<c>0x43e(0)</c> runs over the first n slots, whoever stands there),
    /// and the portrait of each of the first <paramref name="granted"/> granted members over its slot.
    /// </summary>
    public static IEnumerable<(int Slot, bool Portrait)> Drawn(IReadOnlyList<VictorySlot> slots, bool ghosts, int granted,
        int ghostCount = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!ghosts)
            yield break;
        var n = Math.Min(slots.Count(s => s.Granted), ghostCount);
        for (var i = 0; i < n && i < slots.Count; i++)
            yield return (i, false);
        var shown = 0;
        for (var i = 0; i < slots.Count && shown < granted; i++)
            if (slots[i].Granted)
            {
                shown++;
                yield return (i, true);
            }
    }

    /// <summary>
    /// II's row (<c>0x12f2a</c>'s loop over records d7 = 0…n−1, n = OK members): the first <paramref name="presented"/>
    /// records' portraits, each in its own slot, whatever its status (so a DEAD member before an OK one is shown).
    /// </summary>
    public static IEnumerable<(int Slot, bool Portrait)> DrawnByRecord(IReadOnlyList<VictorySlot> slots, int presented)
    {
        ArgumentNullException.ThrowIfNull(slots);
        for (var i = 0; i < presented && i < slots.Count; i++)
            yield return (i, true);
    }
}

/// <summary>
/// The victory screen's sounds and how each holds the screen (s128; the pack's <see cref="EndingSceneSounds"/> with each
/// sound's <see cref="SoundHold"/> from its decoded script, s130). <see cref="Numbers"/> null = no sounds.
/// </summary>
public sealed record VictorySoundTimes(EndingSceneSounds? Numbers, SoundHold BeforeBlack, SoundHold OnBlack, SoundHold BeforeArm,
    SoundHold AfterPortrait, IReadOnlyList<(int Sound, SoundHold Hold)> AfterSpeech)
{
    public static readonly VictorySoundTimes None = new(null, SoundHold.None, SoundHold.None, SoundHold.None, SoundHold.None, []);

    /// <summary>The holds for <paramref name="sounds"/>, each from <paramref name="hold"/>.</summary>
    public static VictorySoundTimes From(EndingSceneSounds sounds, Func<int, SoundHold> hold)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        ArgumentNullException.ThrowIfNull(hold);
        return new(sounds, hold(sounds.BeforeBlack), hold(sounds.OnBlack), hold(sounds.BeforeArm),
            hold(sounds.AfterPortrait), [.. sounds.AfterSpeech.Select(n => (n, hold(n)))]);
    }

    /// <summary>The holds for <paramref name="sounds"/>, each one uncuttable wait of <paramref name="seconds"/>.</summary>
    public static VictorySoundTimes From(EndingSceneSounds sounds, Func<int, double> seconds)
    {
        ArgumentNullException.ThrowIfNull(seconds);
        return From(sounds, n => SoundHold.Fixed(seconds(n)));
    }
}
