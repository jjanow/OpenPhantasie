using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

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

    /// <summary>The sum of squared sample-to-sample steps on the left side: weighs the high frequencies.</summary>
    private static double Roughness(float[] stereo)
    {
        double sum = 0;
        for (var i = 2; i < stereo.Length; i += 2)
            sum += (stereo[i] - stereo[i - 2]) * (double)(stereo[i] - stereo[i - 2]);
        return sum;
    }

    /// <summary>s160: AMIGA AUDIO FILTER reaches Paula through I's sound renderer (sounds, tones, the title tune); off
    /// renders the raw output, on (the default) the filtered one.</summary>
    [Fact]
    public void AmigaDosoundRenderer_FilterSwitch_ReachesPaula()
    {
        var tone = AmigaDosoundScore.Tone(0, 124, 64, 25);
        float[] Render(bool? filter) => (filter is { } f
            ? new AmigaDosoundRenderer(tone, TuneRenderer.PaulaNtscHz, 44100, a500Filter: f)
            : new AmigaDosoundRenderer(tone, TuneRenderer.PaulaNtscHz, 44100)).RenderAll();
        var on = Render(true);
        Assert.Equal(on, Render(null)); // on by default
        Assert.True(Roughness(on) < 0.5 * Roughness(Render(false)));
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

    /// <summary>s163: the STF's C10 low-pass corners from the C070787 rev. C sheet's values (R8 1 kΩ ∥ R9 10 kΩ ∥ R10
    /// 5.1 kΩ ∥ R12 470 Ω · hFE 100, C10 0.1 µF; Hatari's 284 Ω YM pull-up for the rising corner).</summary>
    [Fact]
    public void Ym2149StLowPass_CornersFromTheStfSheet()
    {
        Assert.Equal(759.1, Ym2149.StLoadOhms, 1);
        Assert.Equal(2096.6, Ym2149.PullDownCornerHz, 0);
        Assert.Equal(7694, Ym2149.PullUpCornerHz, 0);
    }

    /// <summary>The low-pass's time constant from a render at the chip's own step rate (2 MHz / 8 = 250 kHz, one step
    /// a sample): a drop from level 15 to 0 decays at the pull-down corner, a rise at the pull-up one. Measured on the
    /// difference to the unfiltered chip (the DC blocker after both is the same).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ym2149StLowPass_StepResponseFollowsTheCorner(bool falling)
    {
        const int rate = 250000;
        float[] Render(bool lowPass)
        {
            var psg = new Ym2149(TuneRenderer.StPsgHz, rate, stLowPass: lowPass);
            psg.Write(7, 0x3F); // tone and noise off: the level is output as is
            psg.Write(8, falling ? 15 : 0);
            var settle = new float[2 * 4000];
            psg.Render(settle, 4000);
            psg.Write(8, falling ? 0 : 15);
            var buf = new float[2 * 200];
            psg.Render(buf, 200);
            return buf;
        }
        var on = Render(true);
        var off = Render(false);
        double Diff(int n) => on[2 * n] - off[2 * n];
        Assert.True(Math.Abs(Diff(0)) > 0.3, $"step {Diff(0)}");
        var span = falling ? 10 : 3;
        var fc = Math.Log(Diff(0) / Diff(span)) / span * rate / (2 * Math.PI);
        var expected = falling ? Ym2149.PullDownCornerHz : Ym2149.PullUpCornerHz;
        Assert.InRange(fc, expected * 0.95, expected * 1.05);
    }

    /// <summary>The low-pass belongs to the ST circuit model only: the s108 linear mix is unchanged.</summary>
    [Fact]
    public void Ym2149StLowPass_NotOnTheLinearMix()
    {
        float[] Render(bool lowPass)
        {
            var psg = new Ym2149(TuneRenderer.StPsgHz, 44100, Ym2149Mixing.Linear, lowPass);
            psg.Write(0, 0x1C);
            psg.Write(1, 0x01);
            psg.Write(7, 0x3E);
            psg.Write(8, 15);
            var buf = new float[2 * 4410];
            psg.Render(buf, 4410);
            return buf;
        }
        Assert.Equal(Render(false), Render(true));
    }

    /// <summary>On the ST circuit a 440 Hz square loses its edges' highest harmonics: the filtered render is smoother
    /// (less sample-to-sample change) than the bare chip's, at the same pitch.</summary>
    [Fact]
    public void Ym2149StLowPass_OnByDefault_SmoothsTheSquare()
    {
        float[] Render(bool? lowPass)
        {
            var psg = lowPass is { } f ? new Ym2149(TuneRenderer.StPsgHz, 44100, stLowPass: f) : new Ym2149(TuneRenderer.StPsgHz, 44100);
            psg.Write(0, 0x1C);
            psg.Write(1, 0x01);
            psg.Write(7, 0x3E);
            psg.Write(8, 15);
            var buf = new float[2 * 4410];
            psg.Render(buf, 4410);
            return buf;
        }
        var on = Render(true);
        Assert.Equal(on, Render(null));
        Assert.True(Roughness(on) < 0.9 * Roughness(Render(false)));
    }

    /// <summary>s163: the first X68000's stage after the YM3012 (CZ-600CE control sheet, <i>Outside X68000</i>):
    /// 12 kΩ ∥ 820 pF feedback, 3.9 kΩ + 4.7 µF input; the later boards (CZ-134 / CZ-147) 4.7 kΩ ∥ 1800 pF.</summary>
    [Fact]
    public void X68000Filter_CornersFromTheFirstModelsSheet()
    {
        Assert.Equal(16174, X68000AudioFilter.LowPassCornerHz, 0);
        Assert.Equal(18813, X68000AudioFilter.LaterModelsLowPassCornerHz, 0);
        Assert.Equal(8.68, X68000AudioFilter.HighPassCornerHz, 2);
        Assert.Equal(Math.Sqrt(0.5), X68000AudioFilter.AnalogueGain(X68000AudioFilter.LowPassCornerHz), 3);
        Assert.Equal(1.0, X68000AudioFilter.AnalogueGain(1000), 2);
    }

    /// <summary>At the OPM's own rate (4 MHz / 64 = 62.5 kHz) the bilinear filter stays within 0.5 dB of the analogue
    /// stage up to 5 kHz and within 1 dB up to its 16.2 kHz corner (−3 dB there).</summary>
    [Theory]
    [InlineData(50, 0.5)]
    [InlineData(1000, 0.5)]
    [InlineData(5000, 0.5)]
    [InlineData(12000, 1.0)]
    [InlineData(16174, 1.0)]
    public void X68000Filter_FollowsTheAnalogueResponse(double hz, double toleranceDb)
    {
        const int rate = 62500;
        var filter = new X68000AudioFilter(rate);
        var gain = SineGain(filter.Next, hz, rate);
        var expected = X68000AudioFilter.AnalogueGain(hz);
        Assert.InRange(20 * Math.Log10(gain / expected), -toleranceDb, toleranceDb);
    }

    /// <summary>s163: the ADPCM path after DAOUT (CZ-134 p. 85): a band-pass of about 270 Hz – 3.7 kHz, near 0.97 at its
    /// peak with the summing stage's level left out.</summary>
    [Fact]
    public void X68000AdpcmFilter_BandFromTheServiceManualValues()
    {
        Assert.Equal(0.964, X68000AdpcmFilter.AnalogueGain(1000), 3);
        Assert.Equal(0.636, X68000AdpcmFilter.AnalogueGain(4000), 3);
        Assert.Equal(0.335, X68000AdpcmFilter.AnalogueGain(100), 3);
        Assert.InRange(X68000AdpcmFilter.AnalogueGain(20000), 0, 0.01);
    }

    /// <summary>At 44.1 kHz (the remaster's output) the bilinear filter stays within 0.5 dB of the analogue path in the band
    /// and within 1.5 dB at 6 kHz, 13 dB down the slope.</summary>
    [Theory]
    [InlineData(150, 0.5)]
    [InlineData(1000, 0.5)]
    [InlineData(2600, 0.5)]
    [InlineData(4000, 0.5)]
    [InlineData(6000, 1.5)]
    public void X68000AdpcmFilter_FollowsTheAnalogueResponse(double hz, double toleranceDb)
    {
        const int rate = 44100;
        var filter = new X68000AdpcmFilter(rate);
        var gain = SineGain(filter.Next, hz, rate);
        Assert.InRange(20 * Math.Log10(gain / X68000AdpcmFilter.AnalogueGain(hz)), -toleranceDb, toleranceDb);
    }

    [Fact]
    public void X68000Filter_RemovesDc()
    {
        var filter = new X68000AudioFilter(62500);
        double y = 0;
        for (var i = 0; i < 62500; i++)
            y = filter.Next(0.5);
        Assert.InRange(Math.Abs(y), 0, 0.01);
    }

    /// <summary>s162: IV's title tune goes through the stage by default: the filtered render differs from the bare chip
    /// and carries less sample-to-sample roughness.</summary>
    [OriginalDataFact]
    public void OpmDrvRenderer_OutputFilterOnByDefault()
    {
        var tune = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!.TitleSequence!.Tune!;
        var score = Assert.IsType<OpmDrvScore>(tune.TryLoad());
        float[] Render(bool? filter)
        {
            var r = filter is { } f ? new OpmDrvRenderer(score, 44100, f) : new OpmDrvRenderer(score, 44100);
            var pcm = new float[2 * 88200];
            r.Render(pcm, 88200);
            return pcm;
        }
        var on = Render(true);
        Assert.Equal(on, Render(null));
        var off = Render(false);
        Assert.NotEqual(on, off);
        Assert.True(Roughness(on) < Roughness(off));
    }
}
