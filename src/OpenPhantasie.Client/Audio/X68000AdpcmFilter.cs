namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s163: the X68000's analogue path after the MSM6258's <c>DAOUT</c>, from Sharp's CZ-134 sheet "16. コントロール基本配線図"
/// (p. 85) `[C]` (<c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> 6.13 "Output filter"):
/// <list type="bullet">
/// <item>C260 4.7 µF couples DAOUT into R234 68 kΩ with C256 560 pF to ground, then a unity-gain Sallen-Key stage round
/// IC227 (MC3403, output to its − input): R233 68 kΩ, R232 68 kΩ into the + input, C255 1200 pF from the middle node to the
/// output, C254 180 pF from the + input to ground: a third-order low-pass (−3 dB near 4 kHz).</item>
/// <item>C232 0.47 µF from the follower's output to R261 33 kΩ to ground and the two pan branches: each 1.5 kΩ (R237 / R242;
/// Q207 / Q208 short its far end to ground when that side is off), C231 / C236 0.47 µF, R230 / R229 2.2 kΩ into the
/// inverting summing stage the OPM shares (R247 / R246 4.7 kΩ ∥ C229 / C228 1800 pF, <see cref="X68000AudioFilter"/>).</item>
/// </list>
/// With both sides on (<c>_voice</c>'s pan 3) the transfer from DAOUT to either output is
/// H(s) = −(Rf / Rs) · Rs s² C232 Cb / (D1(s) · D2(s) · (1 + s Rf Cf)), Rs = 1.5 k + 2.2 k, with
/// D1 = (1 + s C254 (R233 + R232) + s² R233 R232 C255 C254)(1 + s R234 C256) + s R234 C254 (1 + s R232 C255) and
/// D2 = 1/R261 + s (C232 + Cb Rs / R261 + 2 Cb) + s² C232 Cb Rs: a band-pass of about 270 Hz – 3.7 kHz. Modelled with the
/// summing stage's level −Rf / Rs and inversion left out (as the OPM's filter leaves out its −4.7 / 3.9), by the bilinear
/// transform pre-warped at <see cref="WarpHz"/>. Not modelled `[~]`: DAOUT's own output impedance (taken as 0), C260's
/// high-pass (≈ 0.17 Hz into the network's 204 kΩ), the op-amps taken as ideal.
/// </summary>
public sealed class X68000AdpcmFilter
{
    public const double R234 = 68e3, R233 = 68e3, R232 = 68e3, C256 = 560e-12, C255 = 1200e-12, C254 = 180e-12;
    public const double C232 = 0.47e-6, R261 = 33e3, PanR = 1.5e3, PanC = 0.47e-6, SumR = 2.2e3;
    public const double FeedbackR = 4.7e3, FeedbackC = 1800e-12;

    /// <summary>The frequency where the bilinear transform's warp is cancelled (in the low-pass's knee).</summary>
    public const double WarpHz = 3000;

    private readonly double[] _b, _a; // z-domain, a[0] = 1
    private readonly double[] _state;

    public X68000AdpcmFilter(double sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var (num, den) = Analogue();
        var warp = Math.Min(WarpHz, 0.45 * sampleRate);
        var k = 2 * Math.PI * warp / Math.Tan(Math.PI * warp / sampleRate);
        (_b, _a) = Bilinear(num, den, k);
        _state = new double[_a.Length];
    }

    /// <summary>The analogue H(s) (level left out) as ascending coefficients of s: numerator, denominator.</summary>
    public static (double[] Num, double[] Den) Analogue()
    {
        const double rs = PanR + SumR, g = 1 / R261;
        double[] p = [1, C254 * (R233 + R232), R233 * R232 * C255 * C254];
        var d1 = Add(Mul(p, [1, R234 * C256]), Mul([0, R234 * C254], [1, R232 * C255]));
        double[] d2 = [g, C232 + PanC * rs * g + 2 * PanC, C232 * PanC * rs];
        var den = Mul(Mul(d1, d2), [1, FeedbackR * FeedbackC]);
        return ([0, 0, rs * C232 * PanC], den);
    }

    /// <summary>|H| of the analogue path at <paramref name="hz"/> (level left out: about 0.97 at its peak).</summary>
    public static double AnalogueGain(double hz)
    {
        var (num, den) = Analogue();
        var w = 2 * Math.PI * hz;
        return Eval(num, w) / Eval(den, w);

        static double Eval(double[] c, double w)
        {
            double re = 0, im = 0, pr = 1, pi = 0; // (jw)^i
            foreach (var x in c)
            {
                re += x * pr;
                im += x * pi;
                (pr, pi) = (-pi * w, pr * w);
            }
            return Math.Sqrt(re * re + im * im);
        }
    }

    public double Next(double x)
    {
        // Direct form II transposed.
        var y = _b[0] * x + _state[0];
        for (var i = 1; i < _a.Length; i++)
            _state[i - 1] = _b[i] * x - _a[i] * y + (i < _state.Length ? _state[i] : 0);
        return y;
    }

    private static (double[] B, double[] A) Bilinear(double[] num, double[] den, double k)
    {
        var n = den.Length - 1;
        var b = new double[n + 1];
        var a = new double[n + 1];
        void Map(double[] c, double[] into)
        {
            for (var i = 0; i < c.Length; i++)
            {
                // c_i s^i, s = k (1 − z⁻¹) / (1 + z⁻¹), times (1 + z⁻¹)^n.
                var term = new double[] { c[i] * Math.Pow(k, i) };
                for (var j = 0; j < i; j++)
                    term = Mul(term, [1, -1]);
                for (var j = i; j < n; j++)
                    term = Mul(term, [1, 1]);
                for (var j = 0; j < term.Length; j++)
                    into[j] += term[j];
            }
        }
        Map(num, b);
        Map(den, a);
        var a0 = a[0];
        for (var i = 0; i <= n; i++)
        {
            b[i] /= a0;
            a[i] /= a0;
        }
        return (b, a);
    }

    private static double[] Mul(double[] x, double[] y)
    {
        var r = new double[x.Length + y.Length - 1];
        for (var i = 0; i < x.Length; i++)
            for (var j = 0; j < y.Length; j++)
                r[i + j] += x[i] * y[j];
        return r;
    }

    private static double[] Add(double[] x, double[] y)
    {
        var r = new double[Math.Max(x.Length, y.Length)];
        for (var i = 0; i < r.Length; i++)
            r[i] = (i < x.Length ? x[i] : 0) + (i < y.Length ? y[i] : 0);
        return r;
    }
}
