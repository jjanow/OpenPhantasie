using OpenPhantasie.Client.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s226: the 6581 SID model (<see cref="Sid6581"/>): envelope rates and the exponential decay, the waveforms, the noise
/// register, ring modulation, sync, voice 3 off and the volume.
/// </summary>
public class Sid6581Tests
{
    /// <summary>Clock one cycle at a time until <paramref name="done"/>; the cycles it took.</summary>
    private static int CyclesUntil(Sid6581 sid, Func<bool> done, int limit = 50_000_000)
    {
        for (var n = 0; n < limit; n++)
        {
            if (done())
                return n;
            sid.Clock(1);
        }
        throw new InvalidOperationException("not reached");
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 32)]
    [InlineData(8, 392)]
    [InlineData(15, 31251)]
    public void Attack_Counts255Steps_AtTheRatePeriod(int attack, int period)
    {
        var sid = new Sid6581();
        sid.Write(5, attack << 4); // AD
        sid.Write(6, 0xF0);        // sustain 15
        sid.Write(4, 0x11);        // triangle + gate
        var cycles = CyclesUntil(sid, () => sid.EnvelopeOf(0) == 255);
        // The rate counter runs free, so the first step comes within one period.
        Assert.InRange(cycles, 254 * period, 255 * period);
        Assert.Equal(new[] { 9, 32, 63, 95, 149, 220, 267, 313, 392, 977, 1954, 3126, 3907, 11720, 19532, 31251 }, Sid6581.RatePeriods);
    }

    [Fact]
    public void Decay_IsExponential_AndStopsAtTheSustainLevel()
    {
        var sid = new Sid6581();
        sid.Write(5, 0x00); // attack 0, decay 0 (9 cycles a step)
        sid.Write(6, 0x00); // sustain 0, release 0
        sid.Write(4, 0x11);
        CyclesUntil(sid, () => sid.EnvelopeOf(0) == 255);
        var cycles = CyclesUntil(sid, () => sid.EnvelopeOf(0) == 0);
        // 255 → 93 one step each, → 54 two, → 26 four, → 14 eight, → 6 sixteen, → 0 thirty: 756 rate periods.
        const int steps = 162 + 39 * 2 + 28 * 4 + 12 * 8 + 8 * 16 + 6 * 30;
        Assert.InRange(cycles, (steps - 1) * 9, (steps + 1) * 9);
        // Sustain 10 holds at 170.
        var held = new Sid6581();
        held.Write(5, 0x00);
        held.Write(6, 0xA0);
        held.Write(4, 0x11);
        held.Clock(200_000);
        Assert.Equal(170, held.EnvelopeOf(0));
        // Release from there with release 0 reaches 0 and stays.
        held.Write(4, 0x10);
        held.Clock(200_000);
        Assert.Equal(0, held.EnvelopeOf(0));
        Assert.Equal(new int?[] { 1, 2, 4, 8, 16, 30, 1, null },
            new[] { 255, 93, 54, 26, 14, 6, 0, 100 }.Select(Sid6581.ExponentialPeriodAt));
    }

    [Fact]
    public void Envelope_RateCounterWraps_WhenThePeriodDropsBelowIt()
    {
        // The ADSR delay: after counting toward a long period, a shorter one is reached only after the 15-bit wrap.
        var sid = new Sid6581();
        sid.Write(5, 0xF0); // attack 15: 31251 cycles a step
        sid.Write(4, 0x11);
        sid.Clock(20_000);  // the rate counter is past 9 now
        Assert.Equal(0, sid.EnvelopeOf(0));
        sid.Write(5, 0x00); // attack 0: period 9
        var cycles = CyclesUntil(sid, () => sid.EnvelopeOf(0) == 1);
        Assert.InRange(cycles, 32768 - 20_001 + 9 - 2, 32768 - 20_000 + 9 + 2);
    }

    [Fact]
    public void Waveforms_FollowTheAccumulator()
    {
        var sid = new Sid6581();
        sid.Write(0, 0x00);
        sid.Write(1, 0x10); // frequency 0x1000
        sid.Write(4, 0x20); // sawtooth
        sid.Clock(256);     // accumulator 0x100000
        Assert.Equal(0x100, sid.WaveformOf(0));
        sid.Write(4, 0x10); // triangle: bits 22-11
        Assert.Equal(0x200, sid.WaveformOf(0));
        sid.Clock(1024 + 512); // accumulator 0x700000
        Assert.Equal(0xE00, sid.WaveformOf(0));
        sid.Clock(256);        // 0x800000: bit 23 set, the triangle falls
        Assert.Equal(0xFFF, sid.WaveformOf(0));
        sid.Clock(1024);       // 0xC00000
        Assert.Equal(0x7FF, sid.WaveformOf(0));
        sid.Write(2, 0x00);
        sid.Write(3, 0x08);    // pulse width 0x800
        sid.Write(4, 0x40);
        Assert.Equal(0xFFF, sid.WaveformOf(0)); // 0xC00 ≥ 0x800
        sid.Write(3, 0x0D);
        Assert.Equal(0, sid.WaveformOf(0));     // 0xC00 < 0xD00
        sid.Write(4, 0x48);                     // test: held at 0, pulse high
        Assert.Equal(0xFFF, sid.WaveformOf(0));
        sid.Write(4, 0x20);
        Assert.Equal(0, sid.WaveformOf(0));
        sid.Write(4, 0x30);                     // two at once: their AND `[~]`
        sid.Clock(512);
        Assert.Equal(0x200 & 0x400, sid.WaveformOf(0));
    }

    [Fact]
    public void Noise_ShiftsWhenBit19Rises_WithTaps22And17()
    {
        var sid = new Sid6581();
        Assert.Equal(0x7FFFF8, sid.NoiseRegisterOf(0));
        sid.Write(1, 0x10); // frequency 0x1000: bit 19 rises at cycle 128, then every 256
        sid.Write(4, 0x80);
        var expected = 0x7FFFF8;
        for (var k = 1; k <= 40; k++)
        {
            sid.Clock(256);
            expected = ((expected << 1) | (((expected >> 22) ^ (expected >> 17)) & 1)) & 0x7FFFFF;
            Assert.Equal(expected, sid.NoiseRegisterOf(0));
        }
        // The output takes bits 22 20 16 13 11 7 4 2 as bits 11-4.
        var s = expected;
        var bits = new[] { 22, 20, 16, 13, 11, 7, 4, 2 };
        var output = bits.Select((b, i) => ((s >> b) & 1) << (11 - i)).Sum();
        Assert.Equal(output, sid.WaveformOf(0));
        // A maximal-length register: no state repeats within 100 000 shifts and the outputs vary.
        var seen = new HashSet<int>();
        var outs = new HashSet<int>();
        var r = 0x7FFFF8;
        for (var i = 0; i < 100_000; i++)
        {
            Assert.True(seen.Add(r));
            outs.Add(bits.Select((b, j) => ((r >> b) & 1) << (11 - j)).Sum());
            r = ((r << 1) | (((r >> 22) ^ (r >> 17)) & 1)) & 0x7FFFFF;
        }
        Assert.Equal(256, outs.Count);
    }

    [Fact]
    public void RingModulation_FlipsTheTriangle_WithTheSourceVoice()
    {
        var sid = new Sid6581();
        sid.Write(1, 0x10);  // voice 1 0x1000
        sid.Write(4, 0x14);  // triangle + ring (source: voice 3)
        sid.Write(15, 0x80); // voice 3 0x8000: bit 23 after 256 cycles
        sid.Clock(128);      // voice 1 at 0x080000, voice 3 at 0x400000
        Assert.Equal(0x100, sid.WaveformOf(0));
        sid.Clock(128);      // voice 3 at 0x800000: voice 1's fold inverted
        Assert.Equal(0xFFF - 0x200, sid.WaveformOf(0));
        sid.Write(4, 0x10);
        Assert.Equal(0x200, sid.WaveformOf(0));
    }

    [Fact]
    public void Sync_ResetsTheVoice_WhenItsSourceBit23Rises()
    {
        var sid = new Sid6581();
        sid.Write(0, 0x00);
        sid.Write(1, 0x80);  // voice 1 0x8000: bit 23 rises at cycle 256
        sid.Write(7, 0x00);
        sid.Write(8, 0x01);  // voice 2 0x0100
        sid.Write(11, 0x22); // sawtooth + sync (source: voice 1)
        sid.Clock(255);
        Assert.Equal(0x0FF00 >> 12, sid.WaveformOf(1));
        sid.Clock(1);
        Assert.Equal(0, sid.WaveformOf(1));
    }

    [Fact]
    public void Output_FollowsVolume_AndVoice3Off()
    {
        static double Rms(Sid6581 sid, int samples)
        {
            double sum = 0;
            for (var i = 0; i < samples; i++)
            {
                sid.Clock(22);
                var v = sid.TakeSample();
                sum += v * v;
            }
            return Math.Sqrt(sum / samples);
        }
        Sid6581 Voice3(int d418)
        {
            var sid = new Sid6581();
            sid.Write(14, 0x00);
            sid.Write(15, 0x20); // about 480 Hz
            sid.Write(19, 0x00);
            sid.Write(20, 0xF0);
            sid.Write(18, 0x21); // sawtooth + gate
            sid.Write(24, d418);
            Rms(sid, 2000);      // past the attack and the DC blocker's start
            return sid;
        }
        var full = Rms(Voice3(0x0F), 4000);
        var half = Rms(Voice3(0x07), 4000);
        Assert.InRange(full, 0.1, 0.5);
        Assert.InRange(half / full, 7 / 15.0 - 0.02, 7 / 15.0 + 0.02);
        Assert.True(Rms(Voice3(0x8F), 4000) < 1e-4);  // voice 3 off
        Assert.True(Rms(Voice3(0x00), 4000) < 1e-4);  // volume 0
        // Routed through the low-pass, voice 3 off no longer mutes it.
        var filtered = Voice3(0x9F);
        filtered.Write(22, 0xFF);
        filtered.Write(23, 0x04);
        Assert.True(Rms(filtered, 4000) > 0.05);
    }
}
