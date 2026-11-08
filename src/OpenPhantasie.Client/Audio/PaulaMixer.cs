namespace OpenPhantasie.Client.Audio;

/// <summary>
/// The Amiga's Paula as the title tunes use it (s108): four DMA channels, each looping a signed 8-bit sample at a
/// period (Paula clock ticks per sample) and a volume 0–64; channels 0 and 3 are the left output, 1 and 2 the right.
/// Behaviour kept: a new sample pointer given while a channel runs is taken when its buffer wraps (the hardware reloads
/// <c>AUDxLC</c> / <c>AUDxLEN</c> then); a new period takes effect at the next sample; volume at once. The output is the
/// held sample value averaged over each output sample, then (s158, on by default) the A500's analogue output filters
/// per side (<see cref="A500AudioFilter"/>; s160: the game's AMIGA AUDIO FILTER switch, <c>GameSettings.A500Filter</c>,
/// reaches here through the renderers' <c>a500Filter</c>), with the output capacitor's DC removed `[~]`. No MonoGame types: tests
/// render it headless.
/// </summary>
public sealed class PaulaMixer
{
    public const int Channels = 4;

    private readonly Channel[] _ch = [new(), new(), new(), new()];
    private readonly double _ticksPerSample;
    private readonly DcBlocker _left = new(), _right = new();
    private readonly A500AudioFilter? _leftFilter, _rightFilter;

    /// <param name="a500Filter">s158: run the output through the A500's filters (<see cref="A500AudioFilter"/>, the LED
    /// filter in, as Kickstart leaves it and neither Amiga game changes it); false = the raw Paula output.</param>
    public PaulaMixer(double clockHz, int sampleRate, bool a500Filter = true)
    {
        if (clockHz <= 0 || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(clockHz));
        _ticksPerSample = clockHz / sampleRate;
        if (a500Filter)
        {
            _leftFilter = new A500AudioFilter(sampleRate);
            _rightFilter = new A500AudioFilter(sampleRate);
        }
    }

    private sealed class Channel
    {
        public sbyte[]? Sample, Pending;
        public int Pos, Period, Volume;
        public int? PendingPeriod, PendingVolume;
        public double Counter;
        public bool Dma, StopAtWrap;
    }

    public bool IsPlaying(int channel) => _ch[channel].Dma;

    /// <summary>Start <paramref name="sample"/> on <paramref name="channel"/> from its first byte (DMA switched on now).</summary>
    public void Start(int channel, sbyte[] sample, int period, int volume)
    {
        var c = _ch[channel];
        c.Sample = sample;
        c.Pending = null;
        c.PendingPeriod = c.PendingVolume = null;
        c.Period = period;
        c.Volume = volume;
        c.Pos = 0;
        c.Counter = Ticks(period);
        c.Dma = true;
        c.StopAtWrap = false;
    }

    /// <summary>
    /// audio.device's queued write: a running channel switches to it when its buffer wraps (with this period and volume
    /// then); an idle one starts at once.
    /// </summary>
    public void Queue(int channel, sbyte[] sample, int period, int volume)
    {
        var c = _ch[channel];
        if (!c.Dma)
        {
            Start(channel, sample, period, volume);
            return;
        }
        c.Pending = sample;
        c.PendingPeriod = period;
        c.PendingVolume = volume;
        c.StopAtWrap = false;
    }

    /// <summary><c>AUDxLC</c> / <c>AUDxLEN</c>: the sample a running channel takes at its wrap, or the one DMA starts from.</summary>
    public void SetSample(int channel, sbyte[] sample)
    {
        var c = _ch[channel];
        if (c.Dma)
            c.Pending = sample;
        else
            c.Sample = sample;
    }

    public void SetPeriod(int channel, int period) => _ch[channel].Period = period;

    public void SetVolume(int channel, int volume) => _ch[channel].Volume = Math.Clamp(volume, 0, 64);

    /// <summary><c>DMACON</c> set: a channel that is off starts from its sample's first byte; a running one goes on.</summary>
    public void DmaOn(int channel)
    {
        var c = _ch[channel];
        if (c.Dma || c.Sample is null)
            return;
        c.Pos = 0;
        c.Counter = Ticks(c.Period);
        c.Dma = true;
        c.StopAtWrap = false;
    }

    public void DmaOff(int channel)
    {
        _ch[channel].Dma = false;
        _ch[channel].Pending = null;
    }

    /// <summary><c>ADCMD_FINISH</c>: stop at the end of the current buffer.</summary>
    public void FinishAtWrap(int channel)
    {
        var c = _ch[channel];
        if (c.Dma)
            c.StopAtWrap = true;
        c.Pending = null;
    }

    /// <summary>Render <paramref name="frames"/> stereo frames (interleaved L, R) into <paramref name="output"/>.</summary>
    public void Render(Span<float> output, int frames)
    {
        for (var f = 0; f < frames; f++)
        {
            double l = 0, r = 0;
            for (var i = 0; i < Channels; i++)
            {
                var v = Advance(_ch[i]);
                if (i is 0 or 3) l += v; else r += v;
            }
            // Two channels of ±127 × 64 per side.
            l /= 16384.0;
            r /= 16384.0;
            if (_leftFilter is not null)
            {
                l = _leftFilter.Next(l);
                r = _rightFilter!.Next(r);
            }
            output[2 * f] = _left.Next((float)l);
            output[2 * f + 1] = _right.Next((float)r);
        }
    }

    /// <summary>The channel's output (sample × volume) averaged over one output sample.</summary>
    private double Advance(Channel c)
    {
        if (!c.Dma || c.Sample is not { Length: > 0 } s)
            return 0;
        double acc = 0, left = _ticksPerSample;
        while (left > 0 && c.Dma)
        {
            var take = Math.Min(left, c.Counter);
            acc += s[c.Pos] * c.Volume * take;
            c.Counter -= take;
            left -= take;
            if (c.Counter > 0)
                break;
            c.Pos++;
            if (c.Pos >= s.Length)
            {
                c.Pos = 0;
                if (c.StopAtWrap)
                {
                    c.Dma = false;
                    c.StopAtWrap = false;
                    break;
                }
                if (c.Pending is { } next)
                {
                    c.Sample = s = next;
                    c.Pending = null;
                    if (c.PendingPeriod is int p) c.Period = p;
                    if (c.PendingVolume is int vol) c.Volume = vol;
                    c.PendingPeriod = c.PendingVolume = null;
                }
            }
            c.Counter += Ticks(c.Period);
        }
        return acc / _ticksPerSample;
    }

    /// <summary>A period register of 0 counts 65536 ticks.</summary>
    private static double Ticks(int period) => period <= 0 ? 65536 : period;
}

/// <summary>
/// s158: the Amiga 500's audio filters between Paula and the RCA jacks, per side, from Commodore's A500 system schematics
/// (#312511-02 rev 5 and #312511-03 rev 6A/7, sheet 4 "AUDIO FILTERS"; the same values in both):
/// <list type="number">
/// <item>U14's first op-amp stage: feedback R331 / R321 = 360 Ω parallel to C331 / C321 = 0.1 µF, one pole at
/// 1 / (2π · 360 Ω · 0.1 µF) = <see cref="FixedCornerHz"/> ≈ 4421 Hz (always in).</item>
/// <item>The "LED" filter: a unity-gain Sallen-Key low-pass, R332 = R333 = 10 kΩ, C332 = 6800 pF (feedback),
/// C333 = 3900 pF (to ground): f0 = 1 / (2π · 10 kΩ · √(6800 pF · 3900 pF)) = <see cref="LedCornerHz"/> ≈ 3091 Hz,
/// Q = ½ √(6800 / 3900) ≈ 0.660. Two MPF102 JFETs bypass it when /LED is high ("LED off, filters bypassed").
/// Kickstart 1.3 (34.5) writes <c>$BFE001</c> = 2 at reset (LED dim) and clears bit 1 in exec's start-up (ROM
/// <c>0x50c</c>: LED bright, filter in); neither I's <c>game</c> nor III's <c>PhantasieIII</c> touches <c>$BFE001</c>,
/// so the filter is in while they play (Amiga FORMAT.md, "Title tune").</item>
/// </list>
/// Both as bilinear-transform filters prewarped at their corners, DC gain 1 (the stages' own gain and inversion
/// dropped); within 0.5 dB of the analogue response up to the corners, steeper above them (8 kHz at 44.1 kHz: −27 dB
/// for −25 dB). The output coupling (0.22 µF / 22 µF into 1 kΩ + 390 Ω) is the <see cref="DcBlocker"/>'s job `[~]`.
/// </summary>
public sealed class A500AudioFilter
{
    public const double FixedCornerHz = 1.0 / (2 * Math.PI * 360 * 0.1e-6);
    public static readonly double LedCornerHz = 1.0 / (2 * Math.PI * 10e3 * Math.Sqrt(6800e-12 * 3900e-12));
    public static readonly double LedQ = 0.5 * Math.Sqrt(6800.0 / 3900.0);

    private readonly double _p0, _p1;                 // one pole: y = p0 (x + x1) − p1 y1
    private readonly double _b0, _b1, _b2, _a1, _a2;  // Sallen-Key biquad
    private double _x1, _y1, _sx1, _sx2, _sy1, _sy2;

    public A500AudioFilter(int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var k = Math.Tan(Math.PI * Math.Min(FixedCornerHz, 0.45 * sampleRate) / sampleRate);
        _p0 = k / (1 + k);
        _p1 = (k - 1) / (k + 1);
        var w = Math.Tan(Math.PI * Math.Min(LedCornerHz, 0.45 * sampleRate) / sampleRate);
        var norm = 1 / (1 + w / LedQ + w * w);
        _b0 = w * w * norm;
        _b1 = 2 * _b0;
        _b2 = _b0;
        _a1 = 2 * (w * w - 1) * norm;
        _a2 = (1 - w / LedQ + w * w) * norm;
    }

    /// <summary>The analogue gain of both stages at <paramref name="hz"/> (0 … 1), what the filter approximates.</summary>
    public static double AnalogueGain(double hz)
    {
        var a = hz / FixedCornerHz;
        var b = hz / LedCornerHz;
        return 1 / Math.Sqrt(1 + a * a) / Math.Sqrt((1 - b * b) * (1 - b * b) + b / LedQ * (b / LedQ));
    }

    public double Next(double x)
    {
        var y = _p0 * (x + _x1) - _p1 * _y1;
        _x1 = x;
        _y1 = y;
        var z = _b0 * y + _b1 * _sx1 + _b2 * _sx2 - _a1 * _sy1 - _a2 * _sy2;
        _sx2 = _sx1;
        _sx1 = y;
        _sy2 = _sy1;
        _sy1 = z;
        return z;
    }
}

/// <summary>A one-pole DC blocker (the output coupling capacitor `[~]`), corner about 7 Hz at 44.1 kHz.</summary>
internal sealed class DcBlocker
{
    private float _x1, _y1;

    public float Next(float x)
    {
        var y = x - _x1 + 0.999f * _y1;
        _x1 = x;
        _y1 = y;
        return y;
    }
}
