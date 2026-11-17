using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data.Audio;

/// <summary>One write to a SID register (<c>$D400</c> + <see cref="Register"/>) at <see cref="Seconds"/> from the score's start.</summary>
public readonly record struct SidWrite(double Seconds, int Register, int Value);

/// <summary>
/// s226: the C64's sounds of one screen as the programs send them to the SID (MOS 6581): every register write in time
/// order (writes at the same time keep the program's order). No sound rendered yet: the Client plays a score on its SID
/// model (<c>OpenPhantasie.Client.Audio.Sid6581</c>), one chip for the whole screen, so oscillator phases and envelope
/// levels carry from one sound to the next as on the C64.
/// </summary>
public sealed class SidScore : TuneScore
{
    public SidScore(IReadOnlyList<SidWrite> writes, double seconds)
    {
        Writes = writes;
        Seconds = seconds;
    }

    /// <summary>Every write, in time order.</summary>
    public IReadOnlyList<SidWrite> Writes { get; }

    /// <summary>How long the score sounds: its last write plus the pack's <see cref="SidSounds.TailSeconds"/>.</summary>
    public double Seconds { get; }

    public override double DurationSeconds => Seconds;
}

/// <summary>A sound started at <see cref="At"/> seconds of a screen; a running routine (the wind) stops at <see cref="Until"/>.</summary>
public readonly record struct SidCue(double At, string Sound, double Until = double.PositiveInfinity);

/// <summary>
/// s226: a C64 pack's <c>sid_sounds</c> (I / II, <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.6) `[C]`: each
/// sound the end screens' programs make, as the SID register writes they do, in order, with their times. Every sound is
/// plain register writes (no samples, no tune tables). A step is one of:
/// <list type="bullet">
/// <item><c>poke</c>: <c>[register, value]</c> pairs in order; a value <c>{"rnd": [base, span]}</c> is
/// <c>base + INT(RND · span)</c> (BASIC's <c>POKE</c> truncates). s228: <c>every</c> spaces the pokes that many seconds
/// apart and <c>repeat</c> runs the list again (each <c>rnd</c> drawn anew); an empty list marks a sound's end after a
/// closing wait.</item>
/// <item><c>sweep</c>: a BASIC <c>FOR … STEP</c> writing its variable each pass: one register, or two (low byte, high
/// byte); a pass every <c>every</c> seconds (the interpreter's time, measured in VICE). s228: <c>div</c> writes
/// INT(v / div), a <c>STEP</c> of 1 / div.</item>
/// <item><c>zall_wind</c>: <c>ZALL</c>'s KERNAL-IRQ routine (<c>$4D73</c>), run once a jiffy until the cue's
/// <see cref="SidCue.Until"/> (<see cref="WindWrites"/>).</item>
/// <item><c>pick</c> (s228): one of a table's 16-bit values, drawn as BASIC's <c>INT(RND · draw)</c> (the first
/// <c>draw</c> entries), its high byte to the first register and its low byte to the second (the fights' AWAKENS notes).</item>
/// </list>
/// Any step may carry <c>rnd_wait</c> (s228): a BASIC <c>FOR</c> of <c>INT(base + RND · span)</c> empty passes before it,
/// each <c>every</c> seconds; it delays that step and every later step of the sound (MISSES and IS ALIVE hold their gate
/// that long).
/// The values are the programs' numbers (no bytes of the originals beyond them), like the layouts' numbers.
/// </summary>
public sealed class SidSounds
{
    /// <summary>The PAL C64's CPU (and SID) clock, 985 248 Hz (the images hold no region; GRAPHICS.md 9.7).</summary>
    public const double PalClockHz = 985_248;

    private readonly IReadOnlyDictionary<string, IReadOnlyList<Step>> _sounds;

    private SidSounds(double clockHz, double jiffyHz, double tailSeconds, IReadOnlyDictionary<string, IReadOnlyList<Step>> sounds,
        IReadOnlyList<int> marks)
    {
        ClockHz = clockHz;
        JiffyHz = jiffyHz;
        TailSeconds = tailSeconds;
        _sounds = sounds;
        Marks = marks;
    }

    /// <summary>
    /// s228: I / II's <c>MN</c> field 13 (<c>C%</c>) by monster id (0-based): the marks a damage spell draws on that
    /// monster, a <c>fight.*.mark</c> sound each (<c>FOR II = 0 TO C%(WG) − 1</c>); empty when the pack has none.
    /// </summary>
    public IReadOnlyList<int> Marks { get; }

    /// <summary>The marks for monster <paramref name="monsterId"/> (1 when the table lacks it: a FOR runs once).</summary>
    public int MarksFor(int monsterId) => monsterId >= 0 && monsterId < Marks.Count ? Math.Max(1, Marks[monsterId]) : 1;

    /// <summary>The SID's clock (the CPU's): PAL 985 248 Hz.</summary>
    public double ClockHz { get; }

    /// <summary>The KERNAL's interrupt rate (CIA 1 timer A): 60 Hz on PAL and NTSC.</summary>
    public double JiffyHz { get; }

    /// <summary>How long a score keeps sounding after its last write (the releases ring on).</summary>
    public double TailSeconds { get; }

    /// <summary>The sounds' names.</summary>
    public IEnumerable<string> Names => _sounds.Keys;

    public bool Has(string sound) => _sounds.ContainsKey(sound);

    /// <summary>
    /// The writes of <paramref name="cues"/> on one chip, in time order (a cue for a sound the pack lacks is skipped).
    /// <paramref name="random"/> stands for BASIC's <c>RND</c> and the CIA timer bits <c>ZALL</c> reads (0 ≤ r &lt; 1).
    /// </summary>
    public SidScore Score(IEnumerable<SidCue> cues, Func<double> random)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(random);
        var writes = new List<(double At, int Order, int Register, int Value)>();
        var order = 0;
        foreach (var cue in cues.OrderBy(c => c.At))
        {
            if (!_sounds.TryGetValue(cue.Sound, out var steps))
                continue;
            var waited = 0.0;
            foreach (var step in steps)
            {
                if (step.Wait is { } wait)
                    waited += Math.Floor(wait.Base + random() * wait.Span) * wait.Every;
                var at = cue.At + step.At + waited;
                foreach (var (offset, register, value) in step.Expand(cue.Until - at, JiffyHz, random))
                    if (at + offset < cue.Until)
                        writes.Add((at + offset, order++, register, value));
            }
        }
        var sorted = writes.OrderBy(w => w.At).ThenBy(w => w.Order).Select(w => new SidWrite(w.At, w.Register, w.Value)).ToList();
        return new SidScore(sorted, sorted.Count == 0 ? 0 : sorted[^1].Seconds + TailSeconds);
    }

    /// <summary>
    /// s228: how long <paramref name="sound"/> runs from its start to its last write when every <c>rnd_wait</c> draws its
    /// mean (0 for a sound the pack lacks): a cue after it in turn starts then, as the C64's next statement does after a
    /// sound routine returns.
    /// </summary>
    public double Length(string sound)
    {
        if (!_sounds.TryGetValue(sound, out var steps) || steps.Count == 0)
            return 0;
        var waited = 0.0;
        var end = 0.0;
        foreach (var step in steps)
        {
            if (step.Wait is { } wait)
                waited += Math.Floor(wait.Base + wait.Span / 2.0) * wait.Every;
            end = Math.Max(end, step.At + waited + step.Span);
        }
        return end;
    }

    /// <summary>The writes of one sound started at 0 (tests, WAV dumps).</summary>
    public SidScore Score(string sound, Func<double> random, double until = double.PositiveInfinity) =>
        Score([new SidCue(0, sound, until)], random);

    /// <summary>
    /// <c>ZALL</c>'s wind (s226, its 6502 at <c>$4D73</c>, hooked into the KERNAL IRQ at <c>$0314</c>, so once a jiffy)
    /// `[C]`: a state machine over a pitch byte P written to voice 1's (noise) and voice 2's (triangle) frequency high bytes.
    /// <list type="number">
    /// <item>State 3 (start: counter 2): count the counter down; at 0 both gates on, R := 1 + (rnd AND 1), state 0, the
    /// step period := 1 + (rnd AND 3).</item>
    /// <item>State 0, the rise: every period jiffies P + 1, written to both voices; at <see cref="WindParams.High"/> both
    /// gates off, a pause of 1 + (rnd AND 7) jiffies, state 1.</item>
    /// <item>State 1: after the pause the period := 1 + (rnd AND 1), state 2; on the last fall of a gust (the fall counter
    /// = R) voice 2's frequency high byte := 0.</item>
    /// <item>State 2, the fall: every period jiffies P − 1 to voice 1 (and to voice 2 unless it is the last fall); at
    /// <see cref="WindParams.Low"/> the fall counter + 1: while it is ≤ R rise again (state 0, a new period), else state 3
    /// with the counter := (rnd AND 127) + 31 (the carry of the compare before the ADC is set) jiffies of silence.</item>
    /// </list>
    /// The random bits are the CIA 1 timer A's low byte (<c>$DC04</c>) read at that jiffy: modelled as uniform `[~]`.
    /// The IRQ rate is the KERNAL's 60 Hz on PAL as on NTSC; no raster IRQ runs during P.END's wind (UTIL-3.O's animator
    /// starts later, step 7) `[~]`.
    /// </summary>
    public static IEnumerable<(int Jiffy, int Register, int Value)> WindWrites(WindParams p, int jiffies, Func<double> random)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(random);
        int Rnd(int mask) => (int)(random() * 256) & mask;
        int state = 3, pitch = p.Low, period = 0, count = 0, falls = p.StartWait, repeats = 0;
        for (var j = 1; j <= jiffies; j++)
        {
            switch (state)
            {
                case 0:
                    if (--count != 0)
                        break;
                    count = period;
                    pitch++;
                    yield return (j, p.Voice1Freq, pitch);
                    yield return (j, p.Voice2Freq, pitch);
                    if (pitch != p.High)
                        break;
                    state = 1;
                    count = Rnd(7) + 1;
                    yield return (j, p.Voice1Control, p.Voice1Off);
                    yield return (j, p.Voice2Control, p.Voice2Off);
                    break;
                case 1:
                    if (--count != 0)
                        break;
                    state = 2;
                    period = Rnd(1) + 1;
                    count = period;
                    if (falls == repeats)
                        yield return (j, p.Voice2Freq, 0);
                    break;
                case 2:
                    if (--count != 0)
                        break;
                    count = period;
                    pitch--;
                    yield return (j, p.Voice1Freq, pitch);
                    if (falls != repeats)
                        yield return (j, p.Voice2Freq, pitch);
                    if (pitch != p.Low)
                        break;
                    falls++;
                    if (falls <= repeats)
                    {
                        state = 0;
                        period = Rnd(3) + 1;
                        count = period;
                        break;
                    }
                    state = 3;
                    falls = Rnd(127) + 31;
                    break;
                default:
                    if (--falls != 0)
                        break;
                    yield return (j, p.Voice1Control, p.Voice1On);
                    yield return (j, p.Voice2Control, p.Voice2On);
                    repeats = Rnd(1) + 1;
                    state = 0;
                    period = Rnd(3) + 1;
                    count = period;
                    break;
            }
        }
    }

    /// <summary>The wind's numbers (<c>ZALL</c> <c>$4D40</c>–<c>$4E64</c>).</summary>
    public sealed record WindParams(int Low, int High, int StartWait, int Voice1Freq, int Voice2Freq, int Voice1Control,
        int Voice2Control, int Voice1On, int Voice1Off, int Voice2On, int Voice2Off);

    /// <summary>A BASIC <c>FOR 1 TO INT(base + RND · span)</c> of empty passes, <c>every</c> seconds each.</summary>
    public sealed record RndWait(double Base, double Span, double Every);

    private abstract record Step(double At)
    {
        /// <summary>s228: a random wait before this step (and so before every later one).</summary>
        public RndWait? Wait { get; init; }

        /// <summary>How long after <see cref="At"/> the step's last write lands.</summary>
        public virtual double Span => 0;

        /// <summary>The step's writes, each at its offset from <see cref="At"/>, given the time left until the cue stops.</summary>
        public abstract IEnumerable<(double At, int Register, int Value)> Expand(double left, double jiffyHz, Func<double> random);
    }

    /// <summary>The pokes in order, <see cref="Every"/> seconds apart (s228; 0 = together), the list run <see cref="Repeat"/> times.</summary>
    private sealed record PokeStep(double At, IReadOnlyList<(int Register, int Value, int Span)> Pokes, double Every = 0,
        int Repeat = 1) : Step(At)
    {
        public override double Span => Math.Max(0, Pokes.Count * Repeat - 1) * Every;

        public override IEnumerable<(double At, int Register, int Value)> Expand(double left, double jiffyHz, Func<double> random)
        {
            var i = 0;
            for (var n = 0; n < Repeat; n++)
                foreach (var (register, value, span) in Pokes)
                    yield return (i++ * Every, register, span > 0 ? value + (int)Math.Floor(random() * span) : value);
        }
    }

    private sealed record PickStep(double At, int High, int Low, IReadOnlyList<int> Values, int Draw) : Step(At)
    {
        public override IEnumerable<(double At, int Register, int Value)> Expand(double left, double jiffyHz, Func<double> random)
        {
            var v = Values[Math.Min(Values.Count - 1, (int)Math.Floor(random() * Draw))];
            yield return (0, High, (v >> 8) & 0xFF);
            yield return (0, Low, v & 0xFF);
        }
    }

    /// <summary>s228: <see cref="Div"/> &gt; 1 writes INT(v / Div) (a <c>STEP</c> of 1 / Div, as the fights' fade).</summary>
    private sealed record SweepStep(double At, IReadOnlyList<int> Registers, int From, int To, int By, double Every, int Div = 1)
        : Step(At)
    {
        public override double Span => Math.Max(0, Values().Count() - 1) * Every;

        /// <summary>BASIC's FOR: the body runs while the variable has not passed <see cref="To"/>.</summary>
        public IEnumerable<int> Values()
        {
            for (var v = From; By > 0 ? v <= To : v >= To; v += By)
                yield return v;
        }

        public override IEnumerable<(double At, int Register, int Value)> Expand(double left, double jiffyHz, Func<double> random)
        {
            var i = 0;
            foreach (var v in Values())
            {
                var at = i++ * Every;
                if (Registers.Count == 2)
                {
                    yield return (at, Registers[0], v & 0xFF);
                    yield return (at, Registers[1], (v >> 8) & 0xFF);
                }
                else
                    yield return (at, Registers[0], (Div > 1 ? v / Div : v) & 0xFF);
            }
        }
    }

    private sealed record WindStep(double At, WindParams Params) : Step(At)
    {
        public override IEnumerable<(double At, int Register, int Value)> Expand(double left, double jiffyHz, Func<double> random)
        {
            var jiffies = double.IsPositiveInfinity(left) ? (int)(jiffyHz * 600) : (int)Math.Floor(left * jiffyHz);
            foreach (var (j, register, value) in WindWrites(Params, jiffies, random))
                yield return (j / jiffyHz, register, value);
        }
    }

    internal sealed class Dto
    {
        [JsonPropertyName("clock_hz")] public double? ClockHz { get; set; }
        [JsonPropertyName("jiffy_hz")] public double? JiffyHz { get; set; }
        [JsonPropertyName("tail_seconds")] public double? TailSeconds { get; set; }
        [JsonPropertyName("sounds")] public Dictionary<string, List<StepDto>>? Sounds { get; set; }
        [JsonPropertyName("marks")] public List<int>? Marks { get; set; }
    }

    internal sealed class StepDto
    {
        [JsonPropertyName("at")] public double At { get; set; }
        [JsonPropertyName("poke")] public List<JsonElement[]>? Poke { get; set; }
        [JsonPropertyName("every")] public double? Every { get; set; }
        [JsonPropertyName("repeat")] public int? Repeat { get; set; }
        [JsonPropertyName("sweep")] public SweepDto? Sweep { get; set; }
        [JsonPropertyName("zall_wind")] public WindDto? Wind { get; set; }
        [JsonPropertyName("pick")] public PickDto? Pick { get; set; }
        [JsonPropertyName("rnd_wait")] public RndWaitDto? RndWait { get; set; }
    }

    internal sealed class PickDto
    {
        [JsonPropertyName("regs")] public List<int>? Regs { get; set; }
        [JsonPropertyName("values")] public List<int>? Values { get; set; }
        [JsonPropertyName("draw")] public int Draw { get; set; }
    }

    internal sealed class RndWaitDto
    {
        [JsonPropertyName("base")] public double Base { get; set; }
        [JsonPropertyName("span")] public double Span { get; set; }
        [JsonPropertyName("every")] public double Every { get; set; }
    }

    internal sealed class SweepDto
    {
        [JsonPropertyName("regs")] public List<int>? Regs { get; set; }
        [JsonPropertyName("from")] public int From { get; set; }
        [JsonPropertyName("to")] public int To { get; set; }
        [JsonPropertyName("by")] public int By { get; set; }
        [JsonPropertyName("every")] public double Every { get; set; }
        [JsonPropertyName("div")] public int? Div { get; set; }
    }

    internal sealed class WindDto
    {
        [JsonPropertyName("low")] public int Low { get; set; }
        [JsonPropertyName("high")] public int High { get; set; }
        [JsonPropertyName("start_wait")] public int StartWait { get; set; }
        [JsonPropertyName("freq_regs")] public List<int>? FreqRegs { get; set; }
        [JsonPropertyName("control_regs")] public List<int>? ControlRegs { get; set; }
        [JsonPropertyName("gates_on")] public List<int>? GatesOn { get; set; }
        [JsonPropertyName("gates_off")] public List<int>? GatesOff { get; set; }
    }

    /// <summary>The <c>sid_sounds</c> object of a pack manifest's JSON (<paramref name="json"/>: that object alone).</summary>
    public static SidSounds FromJson(string json, string manifestPath) =>
        FromDto(JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidDataException($"{manifestPath}: sid_sounds: empty."),
            manifestPath);

    internal static SidSounds FromDto(Dto dto, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: sid_sounds: {what}.");
        var sounds = new Dictionary<string, IReadOnlyList<Step>>();
        foreach (var (name, stepDtos) in dto.Sounds ?? [])
        {
            var steps = new List<Step>();
            foreach (var s in stepDtos ?? [])
            {
                if (s.At < 0)
                    throw Bad($"{name}: negative 'at'");
                var kinds = (s.Poke is null ? 0 : 1) + (s.Sweep is null ? 0 : 1) + (s.Wind is null ? 0 : 1) + (s.Pick is null ? 0 : 1);
                if (kinds != 1)
                    throw Bad($"{name}: a step is one of poke / sweep / zall_wind / pick");
                RndWait? wait = null;
                if (s.RndWait is { } rw)
                {
                    if (rw.Base < 0 || rw.Span < 0 || rw.Every <= 0)
                        throw Bad($"{name}: rnd_wait needs base, span >= 0 and every > 0");
                    wait = new RndWait(rw.Base, rw.Span, rw.Every);
                }
                var before = steps.Count;
                if (s.Poke is { } pokes)
                {
                    var list = new List<(int, int, int)>();
                    foreach (var pair in pokes)
                    {
                        if (pair is not [{ ValueKind: JsonValueKind.Number } r, var v] || !r.TryGetInt32(out var reg) || reg is < 0 or > 28)
                            throw Bad($"{name}: a poke is [register 0-28, value]");
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var value) && value is >= 0 and <= 255)
                            list.Add((reg, value, 0));
                        else if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("rnd", out var rnd)
                                 && rnd.ValueKind == JsonValueKind.Array && rnd.GetArrayLength() == 2
                                 && rnd[0].TryGetInt32(out var b) && rnd[1].TryGetInt32(out var span) && b >= 0 && span > 0 && b + span <= 256)
                            list.Add((reg, b, span));
                        else
                            throw Bad($"{name}: poke value {v}");
                    }
                    if (s.Every is < 0 || s.Repeat is < 1)
                        throw Bad($"{name}: a poke step's every >= 0 and repeat >= 1");
                    steps.Add(new PokeStep(s.At, list, s.Every ?? 0, s.Repeat ?? 1));
                }
                else if (s.Sweep is { } sw)
                {
                    if (sw.Regs is not ({ Count: 1 } or { Count: 2 }) || sw.Regs.Any(r => r is < 0 or > 24) || sw.By == 0 || sw.Every < 0
                        || sw.Div is < 1 || (sw.Div > 1 && sw.Regs.Count != 1))
                        throw Bad($"{name}: sweep needs 1 or 2 regs, a non-zero 'by', 'every' >= 0 and 'div' >= 1 (one reg)");
                    steps.Add(new SweepStep(s.At, sw.Regs, sw.From, sw.To, sw.By, sw.Every, sw.Div ?? 1));
                }
                else if (s.Wind is { } w)
                {
                    if (w.FreqRegs is not [var f1, var f2] || w.ControlRegs is not [var c1, var c2]
                        || w.GatesOn is not [var on1, var on2] || w.GatesOff is not [var off1, var off2]
                        || w.Low < 0 || w.High <= w.Low || w.High > 255 || w.StartWait <= 0)
                        throw Bad($"{name}: zall_wind needs low < high, start_wait and two freq / control regs, gates_on, gates_off");
                    steps.Add(new WindStep(s.At, new WindParams(w.Low, w.High, w.StartWait, f1, f2, c1, c2, on1, off1, on2, off2)));
                }
                else if (s.Pick is { } pk)
                {
                    if (pk.Regs is not [var hi and >= 0 and <= 24, var lo and >= 0 and <= 24] || pk.Values is not { Count: > 0 } values
                        || values.Any(v => v is < 0 or > 0xFFFF) || pk.Draw < 1 || pk.Draw > values.Count)
                        throw Bad($"{name}: pick needs regs [high, low], 16-bit values and 1 <= draw <= their count");
                    steps.Add(new PickStep(s.At, hi, lo, values, pk.Draw));
                }
                if (wait is not null)
                    steps[before] = steps[before] with { Wait = wait };
            }
            sounds[name] = steps;
        }
        var clock = dto.ClockHz ?? PalClockHz;
        var jiffy = dto.JiffyHz ?? 60;
        if (clock <= 0 || jiffy <= 0)
            throw Bad("clock_hz / jiffy_hz must be positive");
        if (dto.Marks is { } marks && marks.Any(m => m is < 0 or > 8))
            throw Bad("marks are 0-8");
        return new SidSounds(clock, jiffy, Math.Max(0, dto.TailSeconds ?? 0), sounds, dto.Marks ?? []);
    }
}
