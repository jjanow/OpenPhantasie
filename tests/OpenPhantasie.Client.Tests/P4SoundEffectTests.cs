using System.Reflection;
using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s152, IV X68000: the MSM6258 ADPCM decoder (<see cref="Msm6258"/>), <c>VDATA</c>'s effects as <c>_voice(n)</c> cuts
/// them (<see cref="X68kAdpcmEffects"/>), the pack's moment → effect map (<see cref="P4EffectCues"/>, Core's
/// <see cref="P4Sounds"/>) and the renderer at the output rate (FORMAT.md "Sound effects").
/// </summary>
public class P4SoundEffectTests
{
    private static GraphicsPack Pack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!;

    [Fact]
    public void Decoder_KnownVector_LowNibbleFirst()
    {
        // 0x00: +2 (step 16 / 8) twice, index stays 0. 0x17: nibble 7 → +2 +4 +8 +16 = 34, index 8 (step 34);
        // nibble 1 → +4 +8 = 46, index 7. 0x88: nibble 8 → −(31 / 8) = 43, index 6; again → −(28 / 8) = 40.
        Assert.Equal(new short[] { 2, 4, 34, 46, 43, 40 }, Msm6258.Decode([0x00, 0x17, 0x88]));
    }

    [Fact]
    public void Decoder_ClampsTo12Bits()
    {
        Assert.Equal(2047, Msm6258.Decode(Enumerable.Repeat((byte)0x77, 40).ToArray())[^1]);
        Assert.Equal(-2048, Msm6258.Decode(Enumerable.Repeat((byte)0xFF, 40).ToArray())[^1]);
    }

    [Theory]
    [InlineData(0, 3906.25)]
    [InlineData(1, 5208.333)]
    [InlineData(2, 7812.5)]
    [InlineData(4, 15625.0)]
    public void Rates_AreTheIocsTable(int rate, double hz) => Assert.Equal(hz, Msm6258.RateHz(rate), 3);

    [OriginalDataFact]
    public void Pack_LoadsTheEffects_BesideItsArt()
    {
        var pack = Pack();
        Assert.NotNull(pack.TitleSequence); // the art and tune still load with sound_effects present
        Assert.Null(pack.SoundEffects); // not taken for III's driver format
        var fx = pack.AdpcmEffects!;
        Assert.Equal([201, 501, 501, 501, 201, 201, 1001, 1001, 2501], fx.Lengths);
        Assert.Equal(1, fx.Rate);
        Assert.Equal(702, fx.Start(2)); // _sdbeep: bytes 702 ... 1201
        Assert.Equal(6609, fx.Start(9));
    }

    [OriginalDataFact]
    public void Vdata_EveryEffectDecodes_CentredAndInRange()
    {
        var fx = Pack().AdpcmEffects!;
        var image = fx.TryLoadImage()!;
        Assert.Equal(fx.Lengths.Sum(), image.Length); // 6609: _vinit reads it whole
        for (var n = 0; n < fx.Lengths.Count; n++)
        {
            var s = fx.Samples(image, n)!;
            Assert.Equal(2 * (fx.Lengths[n] - 1), s.Length);
            Assert.InRange(s.Average(v => (double)v), -150, 150); // no drift (high nibble first drifts to −1493 on 5)
            Assert.True(s.Max(v => Math.Abs((int)v)) > 400, $"effect {n} is near silent");
        }
        Assert.Null(fx.Bytes(image, 9));
    }

    [OriginalDataFact]
    public void Renderer_HoldsEachSample_AtTheOutputRate()
    {
        var fx = Pack().AdpcmEffects!;
        var image = fx.TryLoadImage()!;
        var r = AdpcmRenderer.For(fx, image, [2], outputFilter: false)!;
        Assert.Equal((long)Math.Ceiling(1000 / (fx.SampleRateHz / 44100)), r.Length); // 1000 samples ≈ 192 ms
        var pcm = r.RenderAll();
        var src = fx.Samples(image, 2)!;
        Assert.Equal(src[0] / 2048f * AdpcmRenderer.Gain, pcm[0]);
        Assert.Equal(pcm[0], pcm[1]); // both channels
        Assert.Equal(src[1] / 2048f * AdpcmRenderer.Gain, pcm[2 * 9]); // 44100 / 5208.3 ≈ 8.47 frames a sample
        Assert.All(pcm, v => Assert.InRange(v, -1f, 1f));
        Assert.InRange(AdpcmRenderer.For(fx, image, [2, 2])!.Length, 2 * r.Length - 1, 2 * r.Length); // in turn
        Assert.Null(AdpcmRenderer.For(fx, image, [42]));
    }

    [OriginalDataFact]
    public void Moments_MapToTheCallersEffects_WalkAlternates()
    {
        var fx = Pack().AdpcmEffects!;
        var cues = new P4EffectCues();
        // _sdwlk: _wlkflg ^= 1 → 4 first, then 5; _omain and _dmove share the flag.
        Assert.Equal([4, 2, 5, 4, 3, 7, 8, 0],
            cues.EffectsFor(fx, [P4Sounds.DungeonStep, P4Sounds.MessageWait, P4Sounds.OutdoorStep, P4Sounds.DungeonStep,
                P4Sounds.EventSound, "no.such.moment", P4Sounds.MonsterDamaged, P4Sounds.MemberCasts, P4Sounds.CombatStep]));
        cues.Enqueue([2]);
        cues.Reset(); // a load: nothing waits, and _wlkflg keeps its turn (0 only at program start)
        Assert.Empty(cues.Waiting);
        Assert.Equal([5, 4], cues.EffectsFor(fx, [P4Sounds.OutdoorStep, P4Sounds.DungeonStep]));
    }

    [OriginalDataFact]
    public void EveryCoreMoment_IsMapped_AndNothingElse()
    {
        var names = typeof(P4Sounds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var mapped = Pack().AdpcmEffects!.Moments;
        Assert.Equal(names.Order(), mapped.Keys.Order());
        // _bsetr (1) and _sdatk (6) have no caller in P4.X: no moment plays them.
        Assert.DoesNotContain(mapped.Values.SelectMany(v => v), e => e is 1 or 6);
    }

    [Fact]
    public void Waiting_KeepsTheNewest()
    {
        var cues = new P4EffectCues();
        cues.Enqueue([0, 1, 2]);
        cues.Enqueue([3, 4, 5]);
        Assert.Equal([2, 3, 4, 5], cues.Waiting);
        Assert.Equal([2, 3, 4, 5], cues.TakeWaiting());
        Assert.Empty(cues.Waiting);
    }
}
