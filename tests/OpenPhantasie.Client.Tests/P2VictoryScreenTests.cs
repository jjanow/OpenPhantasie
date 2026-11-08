using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// II ST's ending (<c>0x12f2a</c>, s128 static; <c>endgame.md</c> §7.1) on I's victory screen: intro page until a key,
/// the fade-in, two header lines (3 s or a key), 2 s, per presented record its title (3 s or a key) and the crowd's
/// ten 330 ms flashes, the wand lines, the closing page until a key, the fade-out.
/// </summary>
public class P2VictoryScreenTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    private static EndingSceneLayout Layout() => Pack().EndingScene!;

    // Five OK members and a DEAD one last (the s80 Hatari run); titles for records 0..4.
    private static readonly VictoryScene Scene = new(
        [new(Race.Human, true), new(Race.Elf, true), new(Race.Dwarf, true), new(Race.Gnome, true), new(Race.Orc, true),
         new(Race.Troll, false)],
        ["speaks", "w1", "w2", "w3", " "])
    {
        IntroPage = ["i1", "i2"],
        HeaderLines = ["speaks", "presenting"],
        Titles = ["t0", "t1", "t2", "t3", "t4"],
        ClosingPage = ["c1"],
    };

    [OriginalDataFact]
    public void Pack_HasTheCodesNumbers()
    {
        var layout = Layout();
        Assert.Null(layout.Ghost);
        Assert.Null(layout.Arm);
        Assert.Equal((60.0, 10, 1000.0, 2000, 3000, 0), (layout.VblHz, layout.FadeVbl, layout.TickHz, layout.HoldTicks, layout.LineTicks, layout.GrantTicks));
        Assert.Equal((150, 75, 158, 192, 310, true), (layout.StageX!.Value, layout.StageY!.Value, layout.RowTop, layout.TextY, layout.JustifyWidth, layout.RowByRecord));
        Assert.Equal("#00FF00", layout.Palette[layout.TextPen]); // VDI 5 → register 6 of `combat` (green, s80 title shot)
        Assert.Equal((40, 127, 10, 330), (layout.Crowd!.X, layout.Crowd.Y, layout.Crowd.Flashes, layout.Crowd.FlashTicks));
        Assert.Equal("#249100", layout.IntroPage!.Palette![layout.IntroPage.TextPen]);   // map register 2 (s80 shot 0x140)
        // s162: the intro page keeps the palette the dungeon left: `map` (0x5cea, DNG8's map view; the cycle 0x1382e runs on
        // the overworld only, FORMAT.md 10.1), so every register is map's; the page shows 0 (the black fill), 2 and 15.
        Assert.Equal(Pack().Palettes["map"], layout.IntroPage.Palette);
        Assert.Equal(("#000000", "#FF0000"), (layout.IntroPage.Palette[0], layout.IntroPage.Palette[layout.IntroPage.ButtonPen]));
        Assert.Null(layout.ClosingPage!.Palette);
        Assert.Equal("#FFB600", layout.Palette[layout.ClosingPage.TextPen]);            // register 4 (s80 shot 0x750)
        var pack = Pack();
        Assert.All([PictureNames.EndingFinal, .. layout.Crowd.Frames], p => Assert.True(File.Exists(pack.Resolve(p))));
    }

    [OriginalDataFact]
    public void Timeline_PagesHeadersPresentationsSpeech()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        var t0 = screen.At(0);
        Assert.Equal((VictoryScreen.EndingPageKind.Intro, 0), (t0.Page, t0.LitPens));
        Assert.Equal(VictoryScreen.EndingPageKind.Intro, screen.At(500).Page); // until a key
        screen.Key(1.0);
        var fade = 16 * 10 / 60.0;
        Assert.Equal((VictoryScreen.EndingPageKind.None, 1), (screen.At(1.0).Page, screen.At(1.0).LitPens));
        Assert.Equal("speaks", screen.At(1.0 + fade).Text);
        Assert.Equal("presenting", screen.At(1.0 + fade + 3).Text);
        Assert.Equal(1.0 + fade + 6 + 2, screen.GhostsAt, 6);       // two lines of 3 s, then evnt_timer(2000)
        Assert.False(screen.At(screen.GhostsAt).Ghosts);             // II has no ghosts or arm

        var p = screen.GhostsAt;                                     // record 0: title, idle portrait on the stage
        var f = screen.At(p);
        Assert.Equal(("t0", 0, false, -1, 0), (f.Text, f.Stage, f.StagePose, f.Crowd, f.Granted));
        f = screen.At(p + 3);                                        // the other pose, crowd frame 0, then 1 after 330 ms
        Assert.Equal((0, true, 0, "t0"), (f.Stage, f.StagePose, f.Crowd, f.Text));
        Assert.Equal(1, screen.At(p + 3 + 0.33).Crowd);
        Assert.Equal(0, screen.At(p + 3 + 0.66).Crowd);
        f = screen.At(p + 3 + 3.3);                                  // record 1's title; record 0 in the row, crowd frame 2
        Assert.Equal(("t1", 1, false, 2, 1), (f.Text, f.Stage, f.StagePose, f.Crowd, f.Granted));

        Assert.Equal(p + 5 * 6.3, screen.SpeechAt, 6);
        f = screen.At(screen.SpeechAt);
        Assert.Equal(("speaks", -1, true, 5), (f.Text, f.Stage, f.StageCleared, f.Granted));
        Assert.Equal(" ", screen.At(screen.SpeechAt + 4 * 3).Text);
        var closing = screen.At(screen.SpeechAt + 5 * 3);
        Assert.Equal((VictoryScreen.EndingPageKind.Closing, (string?)null), (closing.Page, closing.Text));
        Assert.False(screen.At(1e6).Done);                           // the closing page waits for OK
        screen.Key(1e6);
        Assert.Equal(1e6, screen.FadeOutAt, 6);
        var fading = screen.At(1e6 + 10 / 60.0);
        Assert.Equal((VictoryScreen.EndingPageKind.Closing, 2), (fading.Page, fading.DarkPens));
        Assert.True(screen.At(1e6 + fade).Done);
    }

    [OriginalDataFact]
    public void Keys_EndTitleLines_NotTheCheering()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        screen.Key(0); // the intro page's OK
        var p = screen.GhostsAt;
        Assert.Equal(16 * 10 / 60.0 + 8, p, 6);
        screen.Key(p + 1);                                           // ends t0's line: the cheering starts now
        Assert.True(screen.At(p + 1).StagePose);
        screen.Key(p + 2);                                           // during the cheering (evnt_timer): no effect
        Assert.Equal("t0", screen.At(p + 1 + 3.29).Text);
        Assert.Equal("t1", screen.At(p + 1 + 3.31).Text);
    }

    [Fact]
    public void Row_ByRecord_AndSlots()
    {
        // n = 5: gap (320 − 160) / 6 = 26 → 26, 84, 142, 200, 258 (the s80 title shot's three placed heroes).
        Assert.Equal([26, 84, 142, 200, 258, 316], VictoryScreen.SlotXs(Scene.Slots));
        Assert.Equal([(0, true), (1, true), (2, true)], VictoryScreen.DrawnByRecord(Scene.Slots, 3));
        // A DEAD record before an OK one is still presented (the loop runs over records 0..n−1).
        Assert.Equal([(0, true), (1, true)], VictoryScreen.DrawnByRecord([new(Race.Elf, false), new(Race.Human, true)], 2));
    }

    [Fact]
    public void Justified_SpansTheWidthCentred()
    {
        var xs = VictoryScreenView.JustifiedXs("KLEPTO - the bold from Ferronrah", 310);
        Assert.Equal(5, xs[0]);                                      // (320 − 310) / 2
        Assert.InRange(xs[^1] + PixelFont.Measure("h"), 313, 316);   // ends near x 315
        Assert.True(xs[7] - xs[6] > xs[1] - xs[0]);                  // a space widens more than a letter gap
    }

    [OriginalDataFact]
    public void ISceneSteps_Unchanged()
    {
        // I's pack has none of II's parts, so its screen keeps the s127 steps (also pinned by VictoryScreenTests).
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.EndingScene!;
        Assert.Equal((50.0, (int?)null, (EndingCrowd?)null, false, 0, (EndingPage?)null), (i.TickHz, i.StageX, i.Crowd, i.RowByRecord, i.JustifyWidth, i.IntroPage));
    }
    [OriginalDataFact]
    public void CheerSound_StartsWithEachCheer_AndChangesNoTime()
    {
        // s135: 0xb80e(32) after the other pose; Dosound returns at once, so the crowd's frames run over it.
        var quiet = new VictoryScreen(Layout(), Scene);
        var loud = new VictoryScreen(Layout(), Scene, stSounds: true);
        Assert.Empty(quiet.SoundStarts());
        quiet.Key(1.0);
        loud.Key(1.0);
        Assert.Equal((quiet.SpeechAt, quiet.SpeechEnd), (loud.SpeechAt, loud.SpeechEnd));
        Assert.Equal(VictoryScreen.EndingPageKind.Closing, loud.At(loud.SpeechEnd + 100).Page); // until a key
        var ok = loud.SpeechEnd + 5;                                  // OK on the closing page, the same moment on both
        quiet.Key(ok);
        loud.Key(ok);
        Assert.Equal(VictoryScreen.EndingPageKind.Closing, loud.At(ok).Page); // stays up (dimming) through the fade-out
        Assert.True(loud.At(ok + 0.01).DarkPens >= 1);
        Assert.Equal(ok, loud.FadeOutAt, 6);
        Assert.Equal(quiet.FadeOutAt, loud.FadeOutAt, 6);
        Assert.True(double.IsFinite(loud.DoneAt));
        Assert.Equal(quiet.DoneAt, loud.DoneAt, 6);
        var all = loud.SoundStarts();
        Assert.Equal(7, all.Count);                                   // the intro page's OK click, 5 cheers, the closing page's
        Assert.Equal((1.0, 33), (all[0].At, all[0].Sound));           // 0xb7e6 when OK closes the intro page
        Assert.Equal((ok, 33), (all[^1].At, all[^1].Sound));         // and when OK closes the closing page
        Assert.All(all, s => Assert.Equal(double.PositiveInfinity, s.End));
        var starts = all.Skip(1).Take(5).ToList();
        Assert.All(starts, s => Assert.Equal(32, s.Sound));           // one per presented (OK) record
        for (var k = 0; k < 5; k++)
        {
            var at = loud.GhostsAt + 3 + k * 6.3;                     // after each title line (3 s), then 3.3 s of crowd
            Assert.Equal(at, starts[k].At, 6);
            Assert.Equal(0, loud.At(at).Crowd);
        }
    }

    [OriginalDataFact]
    public void CheerSound_TheNext32WaitsForTheLast_WhenKeysCutTheTitles()
    {
        // s163, endgame.md 7.1 step 7.4: 0xb80e(32) waits (0xb8f2, Speed 20) for the running 32, after the other pose. Uncut
        // titles (3 s + 3.3 s of crowd) leave a 4.14 s sound room; a key at each title's start leaves 3.3 s.
        const double len = 4.14;
        var plain = new VictoryScreen(Layout(), Scene, stSounds: true, stSoundSeconds: n => n == 32 ? len : 0);
        plain.Key(1.0);
        var uncut = plain.SoundStarts().Where(s => s.Sound == 32).Select(s => s.At).ToList();
        for (var k = 1; k < uncut.Count; k++)
            Assert.Equal(6.3, uncut[k] - uncut[k - 1], 6); // no wait: as before
        var cut = new VictoryScreen(Layout(), Scene, stSounds: true, stSoundSeconds: n => n == 32 ? len : 0);
        cut.Key(1.0);
        for (var round = 0; round < 40; round++) // a key every 0.25 s cuts each title line as it starts
            cut.Key(cut.GhostsAt + 0.001 + round * 0.25);
        var starts = cut.SoundStarts().Where(s => s.Sound == 32).Select(s => s.At).ToList();
        Assert.Equal(5, starts.Count);
        for (var k = 1; k < starts.Count; k++)
            Assert.True(starts[k] - starts[k - 1] >= len - 1e-6, $"32 #{k} started {starts[k] - starts[k - 1]:F3} s after the last");
        Assert.Contains(starts.Zip(starts.Skip(1)), p => Math.Abs(p.Second - p.First - len) < 1e-6); // the wait bit
        var waiting = cut.At(starts[1] - 0.1);
        Assert.True(waiting.StagePose); // the other pose shows while it waits
    }

    [OriginalDataFact]
    public void StSounds_NeedTheLayoutsNumbers()
    {
        var l = Layout();
        var layout = l with
        {
            Crowd = l.Crowd! with { Sound = null },
            IntroPage = l.IntroPage! with { Sound = null },
            ClosingPage = l.ClosingPage! with { Sound = null },
        };
        Assert.Empty(new VictoryScreen(layout, Scene, stSounds: true).SoundStarts());
    }
    [OriginalDataFact]
    public void CheerSound_RendersAsANoiseSwellOnTheYm()
    {
        var fx = Pack().StDosoundEffects!;
        var image = fx.TryLoadImage()!;
        var renderer = OpenPhantasie.Client.Audio.SoundEffectPlayer.Render(fx, image, 32, 8000)!;
        var pcm = renderer.RenderAll();
        Assert.Equal(2 * (long)Math.Round(207 * 8000 / 50.0), pcm.Length); // stereo frames for 4.14 s
        double Rms(double from, double to)
        {
            int a = 2 * (int)(from * 8000), b = 2 * (int)(to * 8000);
            var sum = 0.0;
            for (var k = a; k < b; k++) sum += pcm[k] * pcm[k];
            return Math.Sqrt(sum / (b - a));
        }
        Assert.True(Rms(1.2, 1.5) > 0.01);               // held up: shape 13 rises over 32 steps of 8 · 0x1000 / 2 MHz (≈ 0.52 s), then holds
        Assert.True(Rms(1.2, 1.5) > 2 * Rms(0.0, 0.05)); // and it rose from near silence
    }
}
