using OpenPhantasie.Client.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s158: the machines' analogue output stages: the A500's filters after Paula (Commodore's schematic values) and the
/// ST's tied YM2149 outputs (the circuit model of the measured STF table).
/// </summary>
public class AudioOutputModelTests
{
    private static double SineGain(Func<double, double> filter, double hz, int rate)
    {
        double peak = 0;
        for (var i = 0; i < rate; i++)
        {
            var y = filter(Math.Sin(2 * Math.PI * hz * i / rate));
            if (i > rate / 2)
                peak = Math.Max(peak, Math.Abs(y));
        }
        return peak;
    }

    [Fact]
    public void A500Filter_CornersFromTheSchematicValues()
    {
        Assert.Equal(4421, A500AudioFilter.FixedCornerHz, 0);
        Assert.Equal(3091, A500AudioFilter.LedCornerHz, 0);
        Assert.Equal(0.660, A500AudioFilter.LedQ, 3);
    }

    /// <summary>Within 0.5 dB of the analogue stages up to the corners; above them the bilinear transform falls faster
    /// (8 kHz at 44.1 kHz: −27 dB for the analogue −25 dB).</summary>
    [Theory]
    [InlineData(100, 0.5)]
    [InlineData(1000, 0.5)]
    [InlineData(3091, 0.5)]
    [InlineData(8000, 3.0)]
    public void A500Filter_FollowsTheAnalogueResponse(double hz, double toleranceDb)
    {
        var filter = new A500AudioFilter(44100);
        var gain = SineGain(filter.Next, hz, 44100);
        var expected = A500AudioFilter.AnalogueGain(hz);
        Assert.InRange(20 * Math.Log10(gain / expected), -toleranceDb, toleranceDb);
    }

    [Fact]
    public void PaulaMixer_FilterOnByDefault_DullsAHighTone()
    {
        // A square 16-byte wave at period 124 (1.8 kHz fundamental, harmonics far above the corners): the filtered
        // output carries less energy than the raw one.
        sbyte[] wave = [127, 127, 127, 127, 127, 127, 127, 127, -127, -127, -127, -127, -127, -127, -127, -127];
        double Energy(bool filter)
        {
            var paula = new PaulaMixer(TuneRenderer.PaulaNtscHz, 44100, filter);
            paula.Start(0, wave, 124, 64);
            var buf = new float[2 * 8820];
            paula.Render(buf, 8820);
            double sum = 0;
            for (var i = 2 * 4410; i < buf.Length; i += 2)
                sum += buf[i] * (double)buf[i];
            return sum;
        }
        Assert.True(Energy(true) < 0.8 * Energy(false));
    }

    [Fact]
    public void Ym2149StOutput_TiedOutputsCompress_AsTheMeasuredTable()
    {
        Assert.Equal(1.0, Ym2149.StOutput(31, 31, 31), 6);
        Assert.Equal(0.0, Ym2149.StOutput(0, 0, 0), 6);
        // Paulo Simoes' STF table (Hatari ym2149_fixed_vol.h): one voice at 15 = 34469 / 65119 = 0.53, two = 55377 / 65119
        // = 0.85; the circuit model is within 6 % of both (a linear sum would give 1/3 and 2/3).
        Assert.InRange(Ym2149.StOutput(0, 0, 31), 0.53 * 0.95, 0.53 * 1.10);
        Assert.InRange(Ym2149.StOutput(0, 31, 31), 0.85 * 0.95, 0.85 * 1.05);
        for (var n = 1; n < 32; n++)
            Assert.True(Ym2149.StOutput(0, 0, n) > Ym2149.StOutput(0, 0, n - 1));
    }
}
