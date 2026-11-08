namespace OpenPhantasie.Client.Audio;

/// <summary>
/// The X68000's analogue stage after the YM3012 DAC, one per side. s163: the first model's values `[C]`, from the
/// CZ-600CE "コントロール回路図" (the schematic appendix of 桑野雅彦 <i>Outside X68000</i>, ソフトバンク 1993, archive.org
/// <c>OutsideX680001993</c>, file <c>Outside_X68000_schematic</c>, scan 0003; the book's text calls the first model
/// CZ-600C, 初代タイプ): each DAC channel's hold capacitor (C47 / C48 0.01 µF) is buffered by an NJM072 follower (IC10)
/// and fed through C29 / C28 4.7 µF and R31 / R32 3.9 kΩ into an NJM072 inverting stage (IC12) with R36 / R35 12 kΩ ∥
/// C38 / C37 820 pF feedback, which the line out (AO) and the volume pot both take. That gives a one-pole low-pass at
/// 1 / (2π · 12 kΩ · 820 pF) ≈ 16.2 kHz and a one-pole high-pass at 1 / (2π · 3.9 kΩ · 4.7 µF) ≈ 8.7 Hz. Modelled with
/// unit pass-band gain (the stage's −12 / 3.9 level and inversion are not), by the bilinear transform with the corners
/// pre-warped (as <see cref="A500AudioFilter"/>). The later boards (s162: Sharp's service manuals CZ-134, X68000
/// CZ-634C / CZ-644C, and CZ-147, X68030) have 4.7 kΩ ∥ 1800 pF there (≈ 18.8 kHz, gain −1.21), the same input.
/// <c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> 6.11.3.
/// </summary>
public sealed class X68000AudioFilter
{
    /// <summary>CZ-600CE R36 / R35 12 kΩ ∥ C38 / C37 820 pF.</summary>
    public const double LowPassCornerHz = 1.0 / (2 * Math.PI * 12000 * 820e-12);

    /// <summary>CZ-600CE R31 / R32 3.9 kΩ in series with C29 / C28 4.7 µF into the stage's virtual ground.</summary>
    public const double HighPassCornerHz = 1.0 / (2 * Math.PI * 3900 * 4.7e-6);

    /// <summary>The later boards' corner (CZ-134 / CZ-147: 4.7 kΩ ∥ 1800 pF), for reference; not used.</summary>
    public const double LaterModelsLowPassCornerHz = 1.0 / (2 * Math.PI * 4700 * 1800e-12);

    private readonly double _l0, _h0, _lp1, _hp1; // y = l0 (x + x1) − lp1 y1; z = h0 (y − y1') − hp1 z1
    private double _x1, _y1, _z1;

    /// <param name="sampleRate">The rate of the samples fed to <see cref="Next"/> (the OPM's own rate in the renderer).</param>
    public X68000AudioFilter(double sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var k = Math.Tan(Math.PI * Math.Min(LowPassCornerHz, 0.45 * sampleRate) / sampleRate);
        _l0 = k / (1 + k);
        _lp1 = (k - 1) / (k + 1);
        var h = Math.Tan(Math.PI * HighPassCornerHz / sampleRate);
        _h0 = 1 / (1 + h);
        _hp1 = (h - 1) / (h + 1);
    }

    /// <summary>The analogue stage's gain at <paramref name="hz"/> relative to its pass band (0 … 1).</summary>
    public static double AnalogueGain(double hz)
    {
        var a = hz / LowPassCornerHz;
        var b = hz / HighPassCornerHz;
        return 1 / Math.Sqrt(1 + a * a) * (b / Math.Sqrt(1 + b * b));
    }

    public double Next(double x)
    {
        var y = _l0 * (x + _x1) - _lp1 * _y1;
        _x1 = x;
        var z = _h0 * (y - _y1) - _hp1 * _z1;
        _y1 = y;
        _z1 = z;
        return z;
    }
}
