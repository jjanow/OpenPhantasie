using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The title tunes on their chip models (s108), rendered headless (no audio device): the length matches the score, the
/// output is not silent, and the chips do what the scores ask. Set <c>PHANTASIE_TUNE_WAV_DIR</c> to also write each
/// tune as a WAV there (the verification dumps).
/// </summary>
public class TitleTuneTests
{
    private const int Rate = 22050;

    private static TitleTune Tune(GameId game) =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game)).Default!.TitleSequence!.Tune!;

    private static double Rms(ReadOnlySpan<float> s, int from, int count)
    {
        double sum = 0;
        for (var i = from; i < from + count; i++)
            sum += s[i] * (double)s[i];
        return Math.Sqrt(sum / count);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1, 53.56)]
    [InlineData(GameId.P2, 64.62)]
    [InlineData(GameId.P3, 2897 / 60.0)]
    public void EachTitleTune_RendersItsWholeLength_AndIsNotSilent(GameId game, double seconds)
    {
        var r = TuneRenderer.For(Tune(game), Rate)!;
        Assert.Equal(seconds, (double)r.Length / Rate, 3);
        var pcm = r.RenderAll();
        Assert.True(r.Finished);
        Assert.All(pcm, v => Assert.InRange(v, -1.5f, 1.5f));
        // Sound in each quarter of the tune (no silent stretch from a decode that stopped early).
        var quarter = pcm.Length / 4;
        for (var q = 0; q < 4; q++)
            Assert.True(Rms(pcm, q * quarter, quarter) > 0.005, $"{game}: quarter {q} is silent");
        if (Environment.GetEnvironmentVariable("PHANTASIE_TUNE_WAV_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"title-{game}.wav"), TuneRenderer.Wav(pcm, Rate));
        }
    }

    /// <summary>s160: the Paula title tunes (I, III) take the AMIGA AUDIO FILTER switch: on by default, off renders a
    /// brighter tune (more sample-to-sample movement) of the same length.</summary>
    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P3)]
    public void PaulaTitleTunes_TakeTheFilterSwitch(GameId game)
    {
        const int frames = 5 * Rate;
        float[] Head(TuneRenderer r)
        {
            var buf = new float[2 * frames];
            r.Render(buf, frames);
            return buf;
        }
        static double Roughness(float[] s)
        {
            double sum = 0;
            for (var i = 2; i < s.Length; i += 2)
                sum += (s[i] - s[i - 2]) * (double)(s[i] - s[i - 2]);
            return sum;
        }
        var on = TuneRenderer.For(Tune(game), Rate)!;
        var off = TuneRenderer.For(Tune(game), Rate, a500Filter: false)!;
        Assert.Equal(on.Length, off.Length);
        var onHead = Head(on);
        Assert.Equal(onHead, Head(TuneRenderer.For(Tune(game), Rate, a500Filter: true)!));
        Assert.True(Roughness(onHead) < Roughness(Head(off)), $"{game}: the filter changes nothing");
    }

    [OriginalDataFact]
    public void Renderer_StopsAtTheEnd_AndRendersNothingMore()
    {
        var r = TuneRenderer.For(Tune(GameId.P3), Rate)!;
        var buf = new float[2 * 1000];
        long total = 0;
        int n;
        while ((n = r.Render(buf, 1000)) > 0)
            total += n;
        Assert.Equal(r.Length, total);
        Assert.Equal(0, r.Render(buf, 1000));
    }

    [Fact]
    public void Paula_PlaysASampleAtClockOverPeriodTimesLength()
    {
        // 16-byte ramp at period 606 (A = 0x153 in tamb's first step) on NTSC: 3579545 / (606 · 16) ≈ 369.2 Hz,
        // the ST's 2 MHz / (16 · 339) = 368.7 Hz. Count rising zero crossings of the right side (channel 2).
        var paula = new PaulaMixer(TuneRenderer.PaulaNtscHz, 44100);
        paula.Start(2, AmigaDosoundScore.Waveform(), 606, 60);
        var buf = new float[2 * 44100];
        paula.Render(buf, 44100);
        var crossings = 0;
        for (var i = 2 * 4410 + 1; i < buf.Length - 2; i += 2)
            if (buf[i] < 0 && buf[i + 2] >= 0)
                crossings++;
        Assert.InRange(crossings / 0.9, 360, 378);
        Assert.Equal(0f, buf[0]); // channel 2 is right only: left stays silent
    }

    [Fact]
    public void Paula_QueuedWrite_WaitsForTheWrap_FinishStopsAtIt()
    {
        var paula = new PaulaMixer(TuneRenderer.PaulaNtscHz, 44100);
        sbyte[] wave = [100, 100, 100, 100];
        paula.Start(0, wave, 4000, 64);
        paula.FinishAtWrap(0);
        Assert.True(paula.IsPlaying(0));
        var buf = new float[2 * 1000];
        paula.Render(buf, 1000); // 4 × 4000 ticks ≈ 197 output samples, then the channel stops
        Assert.False(paula.IsPlaying(0));
    }

    [Fact]
    public void Ym2149_ToneAtTwoMhzOverSixteenTp_AndLevelZeroIsSilent()
    {
        var psg = new Ym2149(TuneRenderer.StPsgHz, 44100);
        psg.Write(0, 0x1C);  // TP 284 → 2e6 / (16 · 284) = 440.1 Hz
        psg.Write(1, 0x01);
        psg.Write(7, 0x3E);  // tone A only
        psg.Write(8, 15);
        var buf = new float[2 * 44100];
        psg.Render(buf, 44100);
        var crossings = 0;
        for (var i = 2 * 4410; i < buf.Length - 2; i += 2)
            if (buf[i] < 0 && buf[i + 2] >= 0)
                crossings++;
        Assert.InRange(crossings / 0.9, 430, 450);

        psg.Write(8, 0);
        psg.Render(buf, 44100);
        Assert.True(Rms(buf, 2 * 22050, 2 * 22050) < 1e-3);
    }

    [Fact]
    public void Ym2149_DecayEnvelope_FallsToZero_AndAWriteOf13RestartsIt()
    {
        var psg = new Ym2149(TuneRenderer.StPsgHz, 44100);
        psg.Write(0, 0x1C);  // a 440 Hz tone on A
        psg.Write(1, 0x01);
        psg.Write(7, 0x3E);
        psg.Write(8, 0x10);  // A follows the envelope
        psg.Write(12, 0x01); // EP 256: 32 steps × 256 / 250 kHz ≈ 33 ms
        psg.Write(13, 0x00); // \___
        Assert.Equal(1f, Ym2149.Level(31));
        Assert.Equal(0f, Ym2149.Level(0));
        var buf = new float[2 * 4410];
        psg.Render(buf, 4410); // 100 ms
        var early = Rms(buf, 0, 2 * 441);           // first 10 ms
        var late = Rms(buf, 2 * 2205, 2 * 2205);    // 50–100 ms: held at 0
        Assert.True(early > 0.05, $"early {early}");
        Assert.True(late < 0.01, $"late {late}"); // only the DC blocker's tail
        psg.Write(13, 0x00);
        psg.Render(buf, 441);
        Assert.True(Rms(buf, 0, 2 * 441) > 0.05);
    }

    // ---- Phantasie IV: OPMDRV on the YM2151 (s109) ----

    /// <summary>A plain sine: algorithm 7, only OP1 (M1) keyed on, TL 0, AR 31 (instant), no decay, MUL 1.</summary>
    private static Ym2151 SineOpm(int kc, int kf = 0, int dt2 = 0)
    {
        var opm = new Ym2151();
        opm.Write(0x20, 0xC0 | 7);  // L + R, FL 0, CON 7
        opm.Write(0x28, kc);
        opm.Write(0x30, kf << 2);
        opm.Write(0x40, 0x01);      // DT1 0, MUL 1
        opm.Write(0x60, 0);         // TL 0
        opm.Write(0x80, 31);        // AR 31
        opm.Write(0xA0, 0);
        opm.Write(0xC0, dt2 << 6);
        opm.Write(0xE0, 0x0F);      // D1L 0, RR 15
        opm.Write(0x08, 1 << 3);    // key on M1 of channel 0
        return opm;
    }

    private static double Frequency(Ym2151 opm, int samples, double rate)
    {
        int crossings = 0, first = -1, last = -1, prev = 0;
        for (var i = 0; i < samples; i++)
        {
            opm.Clock(out var l, out _);
            if (prev < 0 && l >= 0)
            {
                crossings++;
                if (first < 0)
                    first = i;
                last = i;
            }
            prev = l;
        }
        return (crossings - 1) * rate / (last - first);
    }

    [Theory]
    [InlineData(3_579_545, 0x4A, 0, 440.0)]       // the data sheet's A4 at its 3.58 MHz clock
    [InlineData(4_000_000, 0x48, 5, 440.0)]       // OPMDRV's o4a on the X68000: KC 0x48 + KF 5 (driver init 0x14)
    [InlineData(4_000_000, 0x3C, 5, 261.6)]       // o4 c
    public void Ym2151_SineOperator_PitchFromKeyCode(int clock, int kc, int kf, double hz)
    {
        var rate = clock / 64.0;
        var f = Frequency(SineOpm(kc, kf), (int)rate, rate);
        Assert.InRange(f, hz * 0.997, hz * 1.003);
    }

    [Fact]
    public void Ym2151_Dt2_RaisesBy600Cents_AndTheFirstSamplesAreTheLogSineCurve()
    {
        var rate = 4_000_000 / 64.0;
        Assert.InRange(Frequency(SineOpm(0x48, 5, dt2: 1), (int)rate, rate), 440 * 1.410, 440 * 1.418); // 2^(6/12)
        // Deterministic samples: phase step for 440 Hz = 440 / 62500 · 2^20 ≈ 7381.6 (the chip table: 7380), the output a 14-bit
        // sine through the log-sine / exponent tables: sample n ≈ 8180 · sin(2π · ⌊n · step / 1024⌋ / 1024).
        var opm = SineOpm(0x48, 5);
        var got = new int[6];
        for (var i = 0; i < got.Length; i++)
        {
            opm.Clock(out got[i], out var right);
            Assert.Equal(got[i], right);
        }
        for (var i = 0; i < got.Length; i++)
        {
            var idx = (i + 1) * 7380 >> 10;
            var expect = 8180 * Math.Sin(2 * Math.PI * (idx + 0.5) / 1024);
            Assert.InRange(got[i], expect * 0.98 - 4, expect * 1.02 + 4);
        }
    }

    [Fact]
    public void Ym2151_KeyOff_Releases_AndTotalLevel_Attenuates()
    {
        var rate = 62500;
        var opm = SineOpm(0x48, 5);
        double Rms(Ym2151 o, int n)
        {
            double s = 0;
            for (var i = 0; i < n; i++)
            {
                o.Clock(out var l, out _);
                s += (double)l * l;
            }
            return Math.Sqrt(s / n);
        }
        var on = Rms(opm, rate / 10);
        Assert.InRange(on, 8180 / Math.Sqrt(2) * 0.95, 8180 / Math.Sqrt(2) * 1.05);
        opm.Write(0x60, 16);              // TL 16 = 12 dB → a quarter
        var quiet = Rms(opm, rate / 10);
        Assert.InRange(quiet / on, 0.22, 0.28);
        opm.Write(0x08, 0);               // key off, RR 15: gone within a few ms
        Rms(opm, rate / 50);
        Assert.True(Rms(opm, rate / 10) < 1, "released");
    }

    [Fact]
    public void Ym2151_Algorithm0_Modulates_Op4_AndStaysInRange()
    {
        var opm = new Ym2151();
        opm.Write(0x20, 0xC0 | 7 << 3 | 0); // FL 7, CON 0: OP1 → OP2 → OP3 → OP4
        opm.Write(0x28, 0x48);
        foreach (var slot in new[] { 0, 8, 16, 24 })
        {
            opm.Write(0x40 + slot, 1);
            opm.Write(0x60 + slot, 0);
            opm.Write(0x80 + slot, 31);
            opm.Write(0xE0 + slot, 0x0F);
        }
        opm.Write(0x08, 0x0F << 3);
        var peak = 0;
        var crossings = 0;
        var prev = 0;
        for (var i = 0; i < 6250; i++)
        {
            opm.Clock(out var l, out _);
            peak = Math.Max(peak, Math.Abs(l));
            if (prev < 0 && l >= 0)
                crossings++;
            prev = l;
        }
        Assert.InRange(peak, 4000, 8192);
        Assert.True(crossings > 60, $"a modulated tone crosses more often than its 44 cycles: {crossings}");
    }

    [OriginalDataFact]
    public void PhantasieIV_Song0_OnTheYm2151_StartsWithTheFirstNotes_AndIsNotSilent()
    {
        var r = TuneRenderer.For(Tune(GameId.P4), Rate)!;
        Assert.IsType<OpmDrvRenderer>(r);
        Assert.Equal(5100.74, (double)r.Length / Rate, 1);
        // The first round (19.9 s): a 10.368 ms pass, 48 to a quarter at T120.
        var frames = (int)(1920 * 0.010368 * Rate);
        var pcm = new float[2 * frames];
        Assert.Equal(frames, r.Render(pcm, frames));
        Assert.All(pcm, v => Assert.InRange(v, -1f, 1f));
        for (var q = 0; q < 4; q++)
            Assert.True(Rms(pcm, q * pcm.Length / 4, pcm.Length / 4) > 0.01, $"quarter {q} is silent");
        // 0–0.5 s only track 3 plays (piano o3 c / c# sixteenths), from 0.5 s track 2's brass o5 c (523 Hz).
        Assert.True(Rms(pcm, 0, 2 * (int)(0.05 * Rate)) > 0.01);
        if (Environment.GetEnvironmentVariable("PHANTASIE_TUNE_WAV_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "title-P4-round1.wav"), TuneRenderer.Wav(pcm, Rate));
        }
    }

    [Fact]
    public void Settings_TitleTune_IsOnByDefault_AsTheOriginals()
    {
        Assert.True(new GameSettings().TitleTune);
        var table = UiText.For(GameId.P1);
        Assert.Equal("TITLE TUNE", table.Get("options.title_tune"));
        Assert.True(table.Contains("options.title_tune.desc"));
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1, false)] // tamb.dat ends with cover1 (until_step 1)
    [InlineData(GameId.P2, false)] // the ST's tune ends with the composite (until_tune_ends)
    [InlineData(GameId.P3, true)]  // s157: D/M0 plays on over the Initial Menu (FORMAT.md 7.1, capture_s156_p3_title.md)
    [InlineData(GameId.P4, false)] // song 0 stops with the splash
    public void OnlyIIIsTitleTune_PlaysOnAfterTheSplash(GameId game, bool playsOn)
    {
        Assert.Equal(playsOn, Tune(game).PlaysOn);
        Assert.Equal(playsOn, TitleTuneCarry.CarriesOn(Tune(game), playing: true));
        Assert.False(TitleTuneCarry.CarriesOn(Tune(game), playing: false)); // TITLE TUNE off / no device / already over
    }

    [Fact]
    public void CarriedTune_StopsAtItsEnd_TheFirstSound_LeavingTheGame_OrTheSwitch()
    {
        Assert.False(TitleTuneCarry.CarriesOn(null, playing: true)); // TITLE SCREEN off: no splash, so no tune at all
        // Plays on through a new game and a load in III (the same game).
        Assert.False(TitleTuneCarry.Stops(false, GameId.P3, GameId.P3, titleTuneOn: true, effectStarted: false));
        Assert.True(TitleTuneCarry.Stops(true, GameId.P3, GameId.P3, true, false));    // D/M0 ended on its own (2897 VBLs)
        Assert.True(TitleTuneCarry.Stops(false, GameId.P3, GameId.P3, true, true));    // a sound effect: 0x1f52 resets every voice
        Assert.True(TitleTuneCarry.Stops(false, GameId.P3, null, true, false));        // back in the main menu
        Assert.True(TitleTuneCarry.Stops(false, GameId.P3, GameId.P1, true, false));   // another game
        Assert.True(TitleTuneCarry.Stops(false, GameId.P3, GameId.P3, false, false));  // TITLE TUNE switched off
    }

    /// <summary>
    /// s158: a planned round cuts the carried tune at its first sound, not when the fight's round starts (a fight's set-up
    /// does not stop the driver); a round with no sound does not cut it. The flee line's stop 33 counts as a sound.
    /// </summary>
    [Fact]
    public void CarriedTune_CutAtTheRunsFirstSound()
    {
        Assert.Equal(1000 + 25 * 1000.0 / 60, TitleTuneCarry.CutAtMs(1000, [(158, 18), (25, 15)], 60));
        Assert.Null(TitleTuneCarry.CutAtMs(1000, [], 60));
        Assert.Equal(500.0, TitleTuneCarry.Earlier(900, 500));
        Assert.Equal(900.0, TitleTuneCarry.Earlier(900, null));

        string[] log = ["The bandits scatter!"];
        var run = CombatFieldRun.Plan(log, [new CombatLineTag(0, null, null, CombatSwing.None, P3Sounds.Stop)], [], [], [], [],
            _ => false, AmigaCombatSpeed.Normal);
        Assert.Equal([(0, P3Sounds.Stop)], run.SoundPlays(60));
    }

    /// <summary>s158: III's stop 33 (three null voices) silences what the driver plays: a run with 15 then 33 ends at 33.</summary>
    [OriginalDataFact]
    public void P3Stop_SilencesTheRunningSound()
    {
        var effects = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Default!.SoundEffects;
        Assert.NotNull(effects);
        Assert.True(effects!.IsStop(P3Sounds.Stop));
        if (effects.TryLoadImage() is not { } image)
            return; // the extracts are not on this machine
        var alone = effects.Score(image, [(0, 21)])!;
        var stopped = effects.Score(image, [(0, 21), (2, P3Sounds.Stop)])!;
        Assert.True(stopped.Frames < alone.Frames);
    }
}
