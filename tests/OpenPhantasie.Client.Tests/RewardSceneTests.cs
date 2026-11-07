using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// III's gods' reward scene, the parts built in s109: the flashes of the intro (Amiga <c>0x1f30a</c> / <c>0x1b814</c>,
/// anatomy2 shapes 37-39 in the dungeon palette 0x244de; FORMAT.md §2.4) and the reward tune <c>D/M1</c> from the god's
/// picture to the scene's end (<c>0x1efe8</c> / <c>0x1f24a</c>, §3.1).
/// </summary>
public class RewardSceneTests
{
    private static GraphicsPack Amiga =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")
        ?? throw new InvalidOperationException("III Amiga extracts missing");

    private static RewardFlashLayout Flash => Amiga.RewardScene!.Flash!;

    private static RewardScene Scene() => new(true, [Race.Human], ["noises"], ["god"]);

    [OriginalDataFact]
    public void Pack_TheFlashIsShapes37To39_CutInTheDungeonPalette_Colour0Transparent()
    {
        var f = Flash;
        Assert.Equal((50, 36, 120, 3, 36, 22, 6, 8), (f.Count, f.XRange, f.YRange, f.XJitter, f.XMax, f.YJitter, f.YAdd, f.UnitX));
        Assert.Equal((7, 1, 8, 5, 1, 60), (f.WaitStart, f.WaitMin, f.WaitMax, f.WaitStepEvery, f.WaitExtraVbl, f.VblHz));
        var dungeon = Amiga.Palettes["dungeon"].Select(Rgb).ToHashSet();
        var sizes = new List<(int, int)>();
        var all = new HashSet<(int, int, int)>();
        for (var i = 0; i < 3; i++)
        {
            var path = Amiga.Resolve(f.Pictures[i])!;
            Assert.EndsWith($"flash/{37 + i}.png", path.Replace('\\', '/'));
            sizes.Add(TitleSplashTests.PngSize(path));
            using var png = File.OpenRead(path);
            all.UnionWith(TitleSplashTests.PngColours(png)); // RGB; colour 0 is written as transparent black
        }
        Assert.Equal([(16, 14), (32, 17), (32, 23)], sizes);
        Assert.Subset(dungeon, all);
        // Registers 12-15 only (anatomy2's planes): the dungeon palette's greys and white. Cut in the map palette 0x24010
        // (in force only while 0x24070 = 0, which the fight leaves 1) 12 and 13 would be blue and dark green.
        Assert.Equal(new HashSet<(int, int, int)> { (0, 0, 0), (0xB6, 0xB6, 0xB6), (0x6D, 0x6D, 0x6D), (0x92, 0x92, 0x92), (0xFF, 0xFF, 0xFF) }, all);
    }

    private static (int, int, int) Rgb(string hex) =>
        (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));

    [OriginalDataFact]
    public void Plan_RisingRunSpeedsUp_FadingRunSlowsDown_AsThe0x241e8Word()
    {
        var (rising, fading) = Flash.Plan(_ => 0);
        Assert.Equal(50, rising.Count);
        Assert.Equal(50, fading.Count);
        // 0x1f30a(1): 7 → 6 at flash 0, −1 every 5 down to 1; 0x1f30a(0) carries on from 1, +1 every 5 up to 8.
        Assert.Equal([6, 5, 4, 3, 2, 1, 1, 1, 1, 1], rising.Where((_, i) => i % 5 == 0).Select(r => r.Wait));
        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 8, 8, 8], fading.Where((_, i) => i % 5 == 0).Select(r => r.Wait));
        // Three frames of w + 1 VBLs each (0x2af6): 525 and 1035 VBLs, 8.75 s and 17.25 s at 60 Hz.
        Assert.Equal(525, Flash.RunVbl(rising));
        Assert.Equal(1035, Flash.RunVbl(fading));
    }

    [OriginalDataFact]
    public void Plan_Positions_FromTheCallersAndTheSparklesRandomNumbers()
    {
        var (low, _) = Flash.Plan(_ => 0);
        Assert.All(low, f => Assert.Equal((0, 6), (f.X, f.Y)));
        var (high, _) = Flash.Plan(n => n - 1);
        Assert.All(high, f => Assert.Equal((36 * 8, 119 + 21 + 6), (f.X, f.Y))); // x 35 + 2 capped at 36 bytes
        // The order of the calls: the caller's x (mod 36), y (mod 120), then the sparkle's x (mod 3), y (mod 22).
        var asked = new List<int>();
        Flash.Plan(n => { asked.Add(n); return 0; });
        Assert.Equal([36, 120, 3, 22], asked.Take(4));
        Assert.Equal(400, asked.Count);
        // The sparkle and its 32×24 rectangle stay on the 320×200 screen.
        Assert.True(36 * 8 + 32 <= 320 && 146 + 24 <= 200);
    }

    [OriginalDataFact]
    public void At_FramesPileUp_ThenTheNextFlash_ThenNothing()
    {
        var (rising, _) = Flash.Plan(_ => 0);
        Assert.Equal((rising[0], 1), Flash.At(rising, 0));
        Assert.Equal((rising[0], 1), Flash.At(rising, 6.9)); // wait 6: 7 VBLs a frame
        Assert.Equal((rising[0], 2), Flash.At(rising, 7));
        Assert.Equal((rising[0], 3), Flash.At(rising, 20.9));
        Assert.Equal((rising[1], 1), Flash.At(rising, 21));
        Assert.Equal(3, Flash.At(rising, 524.9)!.Value.Frames);
        Assert.Null(Flash.At(rising, 525));
        Assert.Null(Flash.At(rising, -1));
    }

    [OriginalDataFact]
    public void Clock_Page0RunsTheRisingFlashes_Page1TheFading_ThePictureNone()
    {
        var clock = new RewardSceneClock();
        var flash = Flash; // one layout, as the Client keeps its pack's
        var scene = Scene();
        var rolls = 0;
        int Next(int n) { rolls++; return 0; }
        clock.Observe(scene, 0, 10.0, flash, Next);
        Assert.Equal(400, rolls); // both runs rolled once, when the scene first shows
        Assert.Equal(1, clock.FlashAt(10.0)!.Value.Frames);
        Assert.False(clock.RunOver(10.0 + 524 / 60.0));
        Assert.True(clock.RunOver(10.0 + 525 / 60.0));
        Assert.Null(clock.FlashAt(10.0 + 525 / 60.0));

        clock.Observe(scene, 0, 30.0, flash, Next); // the same page again: its clock runs on
        Assert.True(clock.RunOver(30.0));
        clock.Observe(scene, 1, 30.0, flash, Next);
        Assert.Equal(400, rolls);
        Assert.Equal(2, clock.FlashAt(30.0)!.Value.Flash.Wait);
        Assert.False(clock.RunOver(30.0 + 1034 / 60.0));
        Assert.True(clock.RunOver(30.0 + 1035 / 60.0));

        clock.Observe(scene, GodsRewardScene.PicturePage, 50.0, flash, Next);
        Assert.Null(clock.Run);
        Assert.Null(clock.FlashAt(50.0));
        Assert.False(clock.RunOver(99.0)); // Return moves on from the picture, not the clock

        clock.Observe(Scene(), 0, 60.0, flash, Next); // another scene rolls its own flashes
        Assert.Equal(800, rolls);
        Assert.Equal(0, clock.Page);
    }

    [Fact]
    public void Clock_WithoutFlashes_NeverMovesOnByItself()
    {
        var clock = new RewardSceneClock();
        clock.Observe(Scene(), 0, 0, null, _ => 0);
        Assert.Null(clock.FlashAt(1));
        Assert.False(clock.RunOver(1000));
    }

    [OriginalDataFact]
    public void Tune_IsDm1_659Bytes_NoLoop_RendersItsWholeLength()
    {
        var tune = Amiga.RewardScene!.Tune!;
        Assert.Equal(TuneFormats.AmigaP3Music, tune.Format);
        Assert.EndsWith("unpacked/PhantasieIII/D/m1", tune.FilePath.Replace('\\', '/'));
        Assert.Equal((659, 60, 3_579_545), (tune.Length, tune.VblHz, tune.ClockHz));
        Assert.Null(tune.UntilStep);
        var score = (P3MusicScore)tune.TryLoad()!;
        Assert.False(score.Loops);
        Assert.Equal(RewardTuneFrames, score.Frames);
        var r = TuneRenderer.For(tune, 22050)!;
        Assert.Equal(RewardTuneFrames / 60.0, (double)r.Length / 22050, 3);
        var pcm = r.RenderAll();
        Assert.True(r.Finished);
        Assert.Contains(pcm, v => Math.Abs(v) > 0.01f);
    }

    /// <summary><c>D/M1</c>'s length in VBLs: all three voices read <c>FF</c> and go off.</summary>
    private const int RewardTuneFrames = 3073; // 51.2 s at 60 Hz
}
