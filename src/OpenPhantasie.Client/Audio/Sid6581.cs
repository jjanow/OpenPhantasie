namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s226: the C64's MOS 6581 SID for the C64 packs' sounds (<see cref="OpenPhantasie.Data.Audio.SidSounds"/>), clocked
/// one CPU cycle at a time (PAL 985 248 Hz). Written from the chip's public documentation (the 6581 data sheet, the
/// Commodore 64 Programmer's Reference Guide's SID chapter, and the measured envelope and noise behaviour published by
/// the C64 community); no emulator's code was used.
/// <list type="bullet">
/// <item>Three voices, each a 24-bit phase accumulator adding its 16-bit frequency every cycle, whatever its waveform and
/// gate (a voice that does not sound still drives its neighbour's ring modulation and sync). Test bit: the
/// accumulator held at 0, the pulse output high, the noise register reset.</item>
/// <item>Waveforms, 12 bits: triangle (bits 22–11, folded by bit 23; with ring modulation bit 23 XOR the source voice's
/// bit 23), sawtooth (bits 23–12), pulse (high while bits 23–12 ≥ the 12-bit pulse width), noise (the 23-bit shift
/// register, clocked when accumulator bit 19 rises, feedback bit 22 XOR bit 17, output bits 22 20 16 13 11 7 4 2).
/// Two or more waveforms at once: their bitwise AND `[~]` (the real 6581 mixes them in its analogue DAC; none of the
/// decoded sounds uses one). No waveform: 0 `[~]` (the chip's DAC slowly leaks its last value).</item>
/// <item>Ring modulation and hard sync from the voice before (voice 1 ← voice 3, 2 ← 1, 3 ← 2): sync resets the
/// accumulator in the cycle the source's bit 23 rises.</item>
/// <item>The envelope (ADSR): an 8-bit counter stepped by a 15-bit rate counter that counts cycles up to the period of
/// the current rate (<see cref="RatePeriods"/>) and wraps at 2^15 when a write lowers the period below it (the chip's
/// "ADSR delay bug"). Attack counts up 1 a step to 255 (linear), then decay counts down to the sustain level (nibble ×
/// 17), release to 0; on the way down a second counter divides the steps by 1, 2, 4, 8, 16 or 30 by the level
/// (<see cref="ExponentialPeriodAt"/>: the curve). At 0 the counter stays until the next attack. The gate bit starts
/// attack (from the current level) or release.</item>
/// <item>The filter (6581) `[~]`: a two-integrator state-variable filter run each cycle, the 11-bit cutoff through an
/// approximate 6581 curve (about 220 Hz at 0 rising to about 12 kHz; real chips differ from one another) and the
/// resonance nibble as Q 0.71 … 2.4. Low-, band- and high-pass outputs summed by the mode bits; voice 3 off
/// (<c>$D418</c> bit 7) mutes voice 3 when it is not filtered.</item>
/// <item>Output: each voice (waveform − 2048) × envelope / 256, summed, times the volume nibble / 15. The 6581's DC
/// offsets (the "volume register click") are not modelled `[~]`. Averaged over each output sample, then the C64's
/// output coupling capacitor as a DC blocker.</item>
/// </list>
/// No MonoGame types: tests run it headless.
/// </summary>
public sealed class Sid6581
{
    /// <summary>The PAL C64's clock.</summary>
    public const double PalClockHz = 985_248;

    /// <summary>
    /// Cycles per envelope step of each rate nibble: the data sheet's attack times (2 ms … 8 s over 255 steps at 1 MHz),
    /// as measured on the chip (9, 32, 63 … 31251). Decay and release use the same periods (3 × the attack times in the
    /// data sheet's table comes from the exponential divider).
    /// </summary>
    public static readonly int[] RatePeriods = [9, 32, 63, 95, 149, 220, 267, 313, 392, 977, 1954, 3126, 3907, 11720, 19532, 31251];

    private enum Phase { Attack, DecaySustain, Release }

    private sealed class Voice
    {
        public int Freq, PulseWidth, Control;
        public int Acc, Noise = 0x7FFFF8;
        public bool MsbRose;
        public int Attack, Decay, Sustain, Release;
        public int Envelope, RateCounter, RatePeriod = RatePeriods[0], ExpCounter, ExpPeriod = 1;
        public Phase Phase = Phase.Release;

        public bool Test => (Control & 0x08) != 0;
        public bool Ring => (Control & 0x04) != 0;
        public bool Sync => (Control & 0x02) != 0;
    }

    private readonly Voice[] _v = [new(), new(), new()];
    private int _cutoff, _resonance, _filterRoute, _modeVolume;
    private double _w0, _q1 = 1.0 / 0.707;
    private double _bp, _lp;
    private double _sum;
    private int _cycles;
    private double _dcX, _dcY;
    private readonly double _dcPole;

    /// <param name="sampleRate">The output rate, for the DC blocker's pole (about 16 Hz).</param>
    public Sid6581(double clockHz = PalClockHz, int sampleRate = 44100)
    {
        if (clockHz <= 0 || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(clockHz));
        ClockHz = clockHz;
        _dcPole = Math.Exp(-2 * Math.PI * 16 / sampleRate);
        SetCutoff();
    }

    public double ClockHz { get; }

    /// <summary>Voice <paramref name="voice"/>'s envelope counter (0–255), for tests.</summary>
    public int EnvelopeOf(int voice) => _v[voice].Envelope;

    /// <summary>Voice <paramref name="voice"/>'s 12-bit waveform output now, for tests.</summary>
    public int WaveformOf(int voice) => Waveform(voice);

    /// <summary>Voice <paramref name="voice"/>'s noise shift register (23 bits), for tests.</summary>
    public int NoiseRegisterOf(int voice) => _v[voice].Noise;

    /// <summary>Write register <c>$D400</c> + <paramref name="register"/> (0–24; the read-only 25–28 are ignored).</summary>
    public void Write(int register, int value)
    {
        value &= 0xFF;
        if (register is < 0 or > 24)
            return;
        if (register < 21)
        {
            var v = _v[register / 7];
            switch (register % 7)
            {
                case 0: v.Freq = (v.Freq & 0xFF00) | value; break;
                case 1: v.Freq = (v.Freq & 0x00FF) | (value << 8); break;
                case 2: v.PulseWidth = (v.PulseWidth & 0xF00) | value; break;
                case 3: v.PulseWidth = (v.PulseWidth & 0x0FF) | ((value & 0x0F) << 8); break;
                case 4: WriteControl(v, value); break;
                case 5:
                    v.Attack = value >> 4;
                    v.Decay = value & 0x0F;
                    UpdateRate(v);
                    break;
                case 6:
                    v.Sustain = value >> 4;
                    v.Release = value & 0x0F;
                    UpdateRate(v);
                    break;
            }
            return;
        }
        switch (register)
        {
            case 21: _cutoff = (_cutoff & 0x7F8) | (value & 0x07); SetCutoff(); break;
            case 22: _cutoff = (_cutoff & 0x007) | (value << 3); SetCutoff(); break;
            case 23:
                _resonance = value >> 4;
                _filterRoute = value & 0x0F;
                _q1 = 1.0 / (0.707 * Math.Pow(2.4 / 0.707, _resonance / 15.0));
                break;
            case 24: _modeVolume = value; break;
        }
    }

    private static void WriteControl(Voice v, int value)
    {
        var gateWas = (v.Control & 0x01) != 0;
        var gate = (value & 0x01) != 0;
        var testWas = v.Test;
        v.Control = value;
        if (v.Test)
        {
            v.Acc = 0;
            v.Noise = 0x7FFFFF; // `[~]`: the register fills with ones while test is held
        }
        else if (testWas)
            v.Noise = 0x7FFFFF;
        if (gate && !gateWas)
        {
            v.Phase = Phase.Attack;
            UpdateRate(v);
        }
        else if (!gate && gateWas)
        {
            v.Phase = Phase.Release;
            UpdateRate(v);
        }
    }

    private static void UpdateRate(Voice v) =>
        v.RatePeriod = RatePeriods[v.Phase switch
        {
            Phase.Attack => v.Attack,
            Phase.DecaySustain => v.Decay,
            _ => v.Release,
        }];

    /// <summary>
    /// The exponential divider's new period when the envelope counter reaches <paramref name="level"/>: 255 → 1, 93 → 2,
    /// 54 → 4, 26 → 8, 14 → 16, 6 → 30, 0 → 1; null at any other level (the period stays). The chip compares the counter
    /// with these values, so the period is set on the way up (attack) as on the way down.
    /// </summary>
    public static int? ExponentialPeriodAt(int level) => level switch
    {
        0xFF => 1,
        0x5D => 2,
        0x36 => 4,
        0x1A => 8,
        0x0E => 16,
        0x06 => 30,
        0x00 => 1,
        _ => null,
    };

    /// <summary>The cutoff in Hz for the 11-bit register value, the approximate 6581 curve `[~]`.</summary>
    public static double CutoffHz(int fc) => 200 + 11800 / (1 + Math.Exp(-(fc - 1100) / 180.0));

    private void SetCutoff() => _w0 = 2 * Math.Sin(Math.PI * Math.Min(CutoffHz(_cutoff), ClockHz / 8) / ClockHz);

    private int Waveform(int i)
    {
        var v = _v[i];
        var wave = v.Control >> 4;
        if (wave == 0)
            return -1;
        var output = 0xFFF;
        if ((wave & 1) != 0)
        {
            var msb = v.Acc & 0x800000;
            if (v.Ring)
                msb ^= _v[(i + 2) % 3].Acc & 0x800000;
            output &= ((msb != 0 ? ~v.Acc : v.Acc) >> 11) & 0xFFF;
        }
        if ((wave & 2) != 0)
            output &= v.Acc >> 12;
        if ((wave & 4) != 0)
            output &= v.Test || (v.Acc >> 12) >= v.PulseWidth ? 0xFFF : 0;
        if ((wave & 8) != 0)
        {
            var s = v.Noise;
            output &= ((s >> 11) & 0x800) | ((s >> 10) & 0x400) | ((s >> 7) & 0x200) | ((s >> 5) & 0x100)
                      | ((s >> 4) & 0x080) | ((s >> 1) & 0x040) | ((s << 1) & 0x020) | ((s << 2) & 0x010);
        }
        return output;
    }

    /// <summary>Run <paramref name="cycles"/> cycles, adding their output to the current sample.</summary>
    public void Clock(int cycles)
    {
        for (var c = 0; c < cycles; c++)
            Cycle();
    }

    private void Cycle()
    {
        // The oscillators.
        for (var i = 0; i < 3; i++)
        {
            var v = _v[i];
            if (v.Test)
            {
                v.MsbRose = false;
                continue;
            }
            var before = v.Acc;
            v.Acc = (v.Acc + v.Freq) & 0xFFFFFF;
            v.MsbRose = (before & 0x800000) == 0 && (v.Acc & 0x800000) != 0;
            if ((before & 0x080000) == 0 && (v.Acc & 0x080000) != 0)
            {
                var s = v.Noise;
                v.Noise = ((s << 1) | (((s >> 22) ^ (s >> 17)) & 1)) & 0x7FFFFF;
            }
        }
        for (var i = 0; i < 3; i++)
            if (_v[i].Sync && _v[(i + 2) % 3].MsbRose)
                _v[i].Acc = 0;
        // The envelopes.
        for (var i = 0; i < 3; i++)
            Envelope(_v[i]);
        // The mix.
        double direct = 0, filtered = 0;
        for (var i = 0; i < 3; i++)
        {
            var w = Waveform(i);
            var o = w < 0 ? 0 : (w - 0x800) * _v[i].Envelope / 256.0;
            if ((_filterRoute & (1 << i)) != 0)
                filtered += o;
            else if (i != 2 || (_modeVolume & 0x80) == 0)
                direct += o;
        }
        var hp = filtered - _lp - _q1 * _bp;
        _bp += _w0 * hp;
        _lp += _w0 * _bp;
        var f = ((_modeVolume & 0x10) != 0 ? _lp : 0) + ((_modeVolume & 0x20) != 0 ? _bp : 0) + ((_modeVolume & 0x40) != 0 ? hp : 0);
        _sum += (direct + f) * (_modeVolume & 0x0F) / 15.0;
        _cycles++;
    }

    private static void Envelope(Voice v)
    {
        v.RateCounter = (v.RateCounter + 1) & 0x7FFF;
        if (v.RateCounter != v.RatePeriod)
            return;
        v.RateCounter = 0;
        switch (v.Phase)
        {
            case Phase.Attack:
                v.ExpCounter = 0;
                if (v.Envelope < 0xFF)
                    v.Envelope++;
                if (v.Envelope == 0xFF)
                {
                    v.Phase = Phase.DecaySustain;
                    UpdateRate(v);
                }
                break;
            case Phase.DecaySustain:
                if (++v.ExpCounter < v.ExpPeriod)
                    return;
                v.ExpCounter = 0;
                if (v.Envelope != v.Sustain * 0x11 && v.Envelope > 0)
                    v.Envelope--;
                break;
            default:
                if (++v.ExpCounter < v.ExpPeriod)
                    return;
                v.ExpCounter = 0;
                if (v.Envelope > 0)
                    v.Envelope--;
                break;
        }
        if (ExponentialPeriodAt(v.Envelope) is int period)
            v.ExpPeriod = period;
    }

    /// <summary>
    /// The output sample: the mean of the cycles run since the last call (−1…1 for one voice at full scale and volume 15
    /// is about ±0.5), through the DC blocker. 0 when no cycle ran.
    /// </summary>
    public float TakeSample()
    {
        var x = _cycles == 0 ? 0 : _sum / _cycles / 4096.0;
        _sum = 0;
        _cycles = 0;
        var y = x - _dcX + _dcPole * _dcY;
        _dcX = x;
        _dcY = y;
        return (float)y;
    }
}
