namespace OpenPhantasie.Client.Audio;

/// <summary>
/// The Atari ST's Yamaha YM2149 PSG (2 MHz) for Phantasie II's title tune (s108): three square-wave tone channels,
/// one 17-bit noise generator, one envelope generator with 32 steps, a mixer (register 7) and a level per channel
/// (registers 8–10: 0–15 fixed, or bit 4 = follow the envelope). The chip's documented behaviour, stepped at clock / 8:
/// a tone flips every TP steps (f = clock / 16·TP), the noise shifts every 2·NP steps (clock / 16·NP), the envelope
/// moves one of its 32 steps every EP steps (a 16-step AY cycle of 256·EP / clock), and any write to register 13
/// restarts it.
/// <para>Output (s158, <see cref="Ym2149Mixing.StCircuit"/>, the default): on the ST the three outputs are tied together
/// into a 1 kΩ resistor to ground, so they do not add linearly. Each channel at 5-bit step n (a fixed level v uses
/// step 2v + 1, 1 for v = 1) is a pull-up conductance g[n] (steps of 2^¼ through the load, the chip's 1.5 dB), and
/// the output is <c>WARP / (1 + 1 / (gA + gB + gC))</c>: David Savinkoff's circuit model as Hatari's <c>sound.c</c>
/// (<c>YM2149_BuildModelVolumeTable</c>, FOURTH2 = 1.19, WARP = 5/3) builds it from Paulo Simoes' 16 × 16 × 16 table
/// measured on a real STF (<c>ym2149_fixed_vol.h</c>); within about 8 % of that table from level 4 up, levels 1–3 10–23 %
/// high (all below 0.5 % of full scale; atari-st FORMAT.md 7.1).
/// <see cref="Ym2149Mixing.Linear"/>: the three DAC levels averaged (the s108 model). The output is averaged over each
/// output sample, with the DC removed. No MonoGame types: tests render it headless.</para>
/// </summary>
public sealed class Ym2149
{
    private static readonly float[] Dac = BuildDac();
    private static readonly double[] Conductance = BuildConductance();

    /// <summary>Hatari's FOURTH2 (2^¼ rounded, the chip's step) and WARP (5/3, measured 1.659 from the table's maxima).</summary>
    private const double FourthRootOfTwo = 1.19, Warp = 5.0 / 3.0;

    private readonly Ym2149Mixing _mixing;

    private readonly int[] _reg = new int[16];
    private readonly int[] _toneCount = new int[3];
    private readonly bool[] _tone = new bool[3];
    private readonly double _stepsPerSample;
    private readonly DcBlocker _dc = new();
    private double _stepFrac;
    private int _noiseCount, _envCount, _envStep;
    private int _lfsr = 1;
    private bool _noise, _envHolding, _envAttack;

    public Ym2149(double clockHz, int sampleRate, Ym2149Mixing mixing = Ym2149Mixing.StCircuit)
    {
        _mixing = mixing;
        if (clockHz <= 0 || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(clockHz));
        _stepsPerSample = clockHz / 8 / sampleRate;
    }

    /// <summary>Level <paramref name="step"/> (0–31) of the DAC, 0 … 1.</summary>
    public static float Level(int step) => Dac[step];

    private static float[] BuildDac()
    {
        var t = new float[32];
        for (var i = 1; i < 32; i++)
            t[i] = MathF.Pow(10f, (i - 31) * 1.5f / 20f);
        return t;
    }

    /// <summary>
    /// The ST's output for the three channels at 5-bit steps <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>
    /// (0–31) with <see cref="Ym2149Mixing.StCircuit"/>: 0 … 1 (all three at 31 = 1).
    /// </summary>
    public static double StOutput(int a, int b, int c) =>
        Warp / (1.0 + 1.0 / (Conductance[a] + Conductance[b] + Conductance[c])) * (1.0 / MaxStOutput);

    private static readonly double MaxStOutput = Warp / (1.0 + 1.0 / (3 * 0.5));

    /// <summary>
    /// The pull-up conductances of the 32 steps relative to the 1 kΩ load (Hatari <c>YM2149_BuildModelVolumeTable</c>):
    /// step 31 = 1/2, each step down from the one above through the load at 2^¼.
    /// </summary>
    private static double[] BuildConductance()
    {
        var g = new double[32];
        var conductance = 2.0 / 3.0 / (1.0 - 1.0 / Warp) - 2.0 / 3.0; // = 1
        for (var i = 31; i >= 1; i--)
        {
            g[i] = conductance / 2.0;
            conductance = 1.0 / (1.0 - 1.0 / FourthRootOfTwo / (1.0 / conductance + 1.0)) - 1.0;
        }
        g[0] = 1e-8; // off (Hatari: avoids a division by zero)
        return g;
    }

    public int Register(int r) => _reg[r];

    public void Write(int register, int value)
    {
        if (register is < 0 or > 15)
            return;
        _reg[register] = value & 0xFF;
        if (register == 13)
        {
            _envStep = 0;
            _envCount = 0;
            _envHolding = false;
            _envAttack = (value & 4) != 0;
        }
    }

    private int TonePeriod(int ch) => Math.Max(1, _reg[2 * ch] | ((_reg[2 * ch + 1] & 0x0F) << 8));

    private int EnvLevel => _envHolding ? _envHoldLevel : _envAttack ? _envStep : 31 - _envStep;

    private int _envHoldLevel;

    /// <summary>One step at clock / 8.</summary>
    private float Step()
    {
        for (var ch = 0; ch < 3; ch++)
            if (++_toneCount[ch] >= TonePeriod(ch))
            {
                _toneCount[ch] = 0;
                _tone[ch] = !_tone[ch];
            }
        if (++_noiseCount >= 2 * Math.Max(1, _reg[6] & 0x1F))
        {
            _noiseCount = 0;
            var bit = (_lfsr ^ (_lfsr >> 3)) & 1;
            _lfsr = (_lfsr >> 1) | (bit << 16);
            _noise = (_lfsr & 1) != 0;
        }
        if (!_envHolding && ++_envCount >= Math.Max(1, _reg[11] | (_reg[12] << 8)))
        {
            _envCount = 0;
            if (++_envStep >= 32)
                EnvelopeCycleEnd();
        }

        var mixer = _reg[7];
        Span<int> step = stackalloc int[3];
        for (var ch = 0; ch < 3; ch++)
        {
            var toneOn = _tone[ch] || (mixer & (1 << ch)) != 0;
            var noiseOn = _noise || (mixer & (8 << ch)) != 0;
            var vol = _reg[8 + ch];
            step[ch] = !(toneOn && noiseOn) ? 0 : (vol & 0x10) != 0 ? EnvLevel : FixedStep(vol & 15, _mixing);
        }
        return _mixing == Ym2149Mixing.StCircuit
            ? (float)StOutput(step[0], step[1], step[2])
            : (Dac[step[0]] + Dac[step[1]] + Dac[step[2]]) / 3f;
    }

    /// <summary>A fixed level's 5-bit step: 2v + 1, 0 = off; with the ST circuit 1 stays 1 (Hatari <c>YmVolume4to5</c>,
    /// matching the measured table's level 1).</summary>
    private static int FixedStep(int v, Ym2149Mixing mixing) =>
        v == 0 ? 0 : v == 1 && mixing == Ym2149Mixing.StCircuit ? 1 : 2 * v + 1;

    private void EnvelopeCycleEnd()
    {
        var shape = _reg[13];
        bool cont = (shape & 8) != 0, attack = (shape & 4) != 0, alt = (shape & 2) != 0, hold = (shape & 1) != 0;
        if (!cont)
        {
            _envHolding = true;
            _envHoldLevel = 0;
        }
        else if (hold)
        {
            _envHolding = true;
            _envHoldLevel = attack ^ alt ? 31 : 0;
        }
        else
        {
            if (alt)
                _envAttack = !_envAttack;
            _envStep = 0;
        }
    }

    /// <summary>Render <paramref name="frames"/> stereo frames (the mono sum on both sides).</summary>
    public void Render(Span<float> output, int frames)
    {
        for (var f = 0; f < frames; f++)
        {
            var before = (int)_stepFrac;
            _stepFrac += _stepsPerSample;
            var n = (int)_stepFrac - before;
            float acc = 0;
            for (var i = 0; i < n; i++)
                acc += Step();
            if (_stepFrac > 1e9)
                _stepFrac -= Math.Floor(_stepFrac);
            var v = _dc.Next(n > 0 ? acc / n : 0f);
            output[2 * f] = v;
            output[2 * f + 1] = v;
        }
    }
}

/// <summary>How <see cref="Ym2149"/> combines its three channels (s158).</summary>
public enum Ym2149Mixing
{
    /// <summary>The ST's tied outputs into 1 kΩ (Savinkoff's circuit model of Simoes' measured table), the default.</summary>
    StCircuit,

    /// <summary>The three 1.5-dB DAC levels averaged (the s108 model).</summary>
    Linear,
}
