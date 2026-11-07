using OpenPhantasie.Client;
using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s128, I Amiga's victory screen with its sounds (h32 <c>+0x6d0</c>; h89 <c>+0x304</c> blocks, so each sound holds the
/// screen): 22 before the black, 25 on black, 1 between the ghosts and the arm, 21 after each portrait, 24 and 22
/// before the fade-out.
/// </summary>
public class VictorySoundTests
{
    private static GraphicsPack Pack => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static readonly VictoryScene Scene = new(
        [new(Race.Human, true), new(Race.Elf, false), new(Race.Dwarf, true)], ["one", "two", "three"]);

    private static VictoryScreen Screen()
    {
        var pack = Pack;
        var fx = pack.DosoundEffects!;
        var image = fx.TryLoadImage()!;
        return new VictoryScreen(pack.EndingScene!, Scene,
            VictorySoundTimes.From(pack.EndingScene!.Sounds!, n => fx.Hold(image, n)));
    }

    [OriginalDataFact]
    public void Pack_NamesTheSoundsInTheCodesOrder()
    {
        var s = Pack.EndingScene!.Sounds!;
        Assert.Equal((22, 25, 1, 21), (s.BeforeBlack, s.OnBlack, s.BeforeArm, s.AfterPortrait));
        Assert.Equal([24, 22], s.AfterSpeech);
    }

    // s153 live: each step its ticks / 50 + 3.7 ms per channel it sends (22 / 21: three, 25 / 1 / 24: two), each call
    // 7.5 ms more. F: one blit's WaitTOF (60 Hz).
    private const double Ch = 0.0037, Call = 0.0075, F = 1 / 60.0;
    private const double S22 = 0.6 + 3 * Ch + Call, S25 = 2.5 + 125 * 2 * Ch + Call, S1 = 0.14 + 2 * Ch + Call;
    private const double S21 = 0.62 + 15 * 3 * Ch + Call, S24 = S1;
    private const double Read = 3.5, Save = 6.09;

    [OriginalDataFact]
    public void Hold_StepsAndOverhead_MatchTheCapture()
    {
        var fx = Pack.DosoundEffects!;
        var image = fx.TryLoadImage()!;
        Assert.Equal(S25, fx.Hold(image, 25).Seconds, 6);   // live 3.43 (audible), s130 3.45
        Assert.InRange(fx.Hold(image, 21).Seconds, 0.78, 0.80); // live 0.79 (3.207 − Delay(120) − a blit)
        Assert.InRange(fx.Hold(image, 1).Seconds, 0.15, 0.167);  // live: ghost 6 → arm 0.167 with a WaitTOF
        Assert.InRange(fx.Hold(image, 17).Seconds, 1.03, 1.07);  // live black 1.08 with the fill's frame
        Assert.InRange(fx.Hold(image, 16).Seconds, 0.27, 0.29);  // live: monster erased → "DIES" 0.284
        Assert.InRange(fx.Hold(image, 3).Seconds, 0.21, 0.233); // live: idle shape → "HITS" 0.233
        Assert.InRange(fx.Hold(image, 8).Seconds, 0.083, 0.10); // live: idle shape → "MISSES" 0.10
        Assert.Equal(125, fx.Hold(image, 25).StepEnds.Count);
        Assert.Equal(2.5, fx.Seconds(image, 25), 6);         // the sound itself
        Assert.Equal(3 * Ch + 7 / 50.0 + Call, fx.Hold(image, 21).CutAt(0.05), 6); // a key ends it after step 1
        Assert.Same(SoundHold.None, fx.Hold(image, 33)); // no sound 33
    }

    [OriginalDataFact]
    public void Timeline_EachSoundHoldsTheScreen()
    {
        var plain = new VictoryScreen(Pack.EndingScene!, Scene);
        var screen = Screen();
        Assert.Equal(S22 + Read + S25, screen.FadeInAt, 6);   // 22, final.pic read, 25 (live: 7.58 s)
        Assert.Equal(0, screen.At(screen.FadeInAt - 0.01).LitPens); // still black
        Assert.Equal(1, screen.At(screen.FadeInAt).LitPens);
        Assert.Equal(plain.GhostsAt + S22 + S25, screen.GhostsAt, 6);
        Assert.Equal(screen.GhostsAt + 2 * F + S1, screen.ArmAt, 6); // two ghosts, one a frame, then sound 1
        Assert.Equal(1, screen.At(screen.GhostsAt).GhostCount);
        Assert.Equal(2, screen.At(screen.GhostsAt + F).GhostCount);
        var between = screen.At(screen.GhostsAt + 0.1);
        Assert.True(between.Ghosts);
        Assert.False(between.Arm);
        Assert.Equal(0, between.Granted);
        Assert.True(screen.At(screen.ArmAt).Arm);
        Assert.Equal(0, screen.At(screen.ArmAt).Granted);            // the arm's blit frame first
        Assert.Equal(1, screen.At(screen.ArmAt + F).Granted);        // portrait 0 one frame later (live)
        var period = F + S21 + 2.4;                                    // blit, 21, Delay(120) (live 3.20)
        Assert.Equal(2, screen.At(screen.ArmAt + F + period + 0.001).Granted);
        Assert.Equal(screen.ArmAt + F + 2 * period, screen.SpeechAt, 6);
        Assert.Equal(screen.SpeechEnd + S24 + Save + S22, screen.FadeOutAt, 6); // 24, guild.dat, 22 (live 6.87)
        Assert.Equal(2, screen.At(screen.FadeOutAt - 0.01).Line);
        Assert.Equal(0, screen.At(screen.FadeOutAt - 0.01).DarkPens);
    }

    [OriginalDataFact]
    public void SoundStarts_InOrder_AndAKeyMovesTheLastOnes()
    {
        var screen = Screen();
        var starts = screen.SoundStarts();
        Assert.Equal([22, 25, 1, 21, 21, 24, 22], starts.Select(s => s.Sound));
        double[] want = [0, S22 + Read, screen.GhostsAt + 2 * F, screen.ArmAt + 2 * F,
            screen.ArmAt + 2 * F + S21 + 2.4 + F, screen.SpeechEnd, screen.SpeechEnd + S24 + Save];
        for (var i = 0; i < want.Length; i++)
            Assert.Equal(want[i], starts[i].At, 6);
        Assert.Equal(S22, starts[0].End, 6);
        var end = screen.SpeechEnd;
        screen.Key(screen.SpeechAt + 1);
        Assert.Equal(end - (180 * 0.02187 - 1), screen.SoundStarts()[5].At, 6);
        Assert.Empty(new VictoryScreen(Pack.EndingScene!, Scene).SoundStarts());
    }

    [OriginalDataFact]
    public void OnlyACutSound_IsStoppedAtItsEnd()
    {
        // s156 (Client on Xvfb, recorded from before its start): stopping every sound at its scheduled end clipped 22 to
        // 0.43 s audible after a slow first frame; now an uncut sound plays out (0.65 s recorded), a key's cut still stops.
        var screen = Screen();
        Assert.False(screen.SoundCut(0)); // 22, nobody pressed
        var last = screen.SoundStarts()[4];
        screen.Key(last.At + 0.2);       // ends the last portrait's 21 at its step
        Assert.True(screen.SoundCut(4));
        Assert.False(screen.SoundCut(3));
        Assert.False(screen.SoundCut(99));
    }

    [OriginalDataFact]
    public void Release_InACutSoundsTail_StaysQueued_ForTheNextLine()
    {
        // s153: h89 +0x304 reads one message a step and returns at the cut; a release in the rest of that step stays in the
        // port. After the last portrait's 21 no sound drains it, so it ends speech line 0 at its first poll.
        var screen = Screen();
        var last = screen.SoundStarts()[4];                    // the last portrait's 21
        Assert.Equal(21, last.Sound);
        screen.Key(last.At + 0.2);                             // ends 21 at its step's end
        screen.Release(last.At + 0.205);                       // still inside that step
        var at = screen.SpeechAt;
        Assert.Equal(0, screen.At(at).Line);
        Assert.Equal(1, screen.At(at + 0.02187 + 0.001).Line);  // line 0 took the queued release
        Assert.Equal(1, screen.At(at + 0.02187 + 3.0).Line);    // line 1 runs full
    }

    [OriginalDataFact]
    public void Click_IsAPressAndARelease_EndingTwoLines()
    {
        // s153 v2 live: a click in line 5 ended it (the press) and line 6 (the release).
        var screen = Screen();
        var at = screen.SpeechAt;
        screen.Key(at + 1.0);
        screen.Release(at + 1.1);
        Assert.Equal(2, screen.At(at + 1.1).Line);
    }

    [OriginalDataFact]
    public void Key_InASound_EndsItAtAStep()
    {
        var screen = Screen();
        var on = screen.SoundStarts()[1].At;                 // sound 25
        screen.Key(on + 1.36);                                 // r3: 2.03 s early live
        var cut = 50 * (0.02 + 2 * Ch) + Call;                 // step 50 ends at 1.370 s, then the call's tail
        Assert.Equal(on + cut, screen.FadeInAt, 6);
        Assert.Equal(on + cut, screen.SoundStarts()[1].End, 6);
        screen.Release(on + 1.37);                             // the same sound: already ended; queued, sound 1 drains it
        Assert.Equal(on + cut, screen.FadeInAt, 6);

        var arm = screen.ArmAt;
        screen.Key(arm + 0.38);                                // r4: in sound 21, cut 0.40 s early live
        // 21 starts two frames after the arm shows (the arm's blit, portrait 0's); its steps: 7, 4, 3, 3, 2 … ticks + 3
        // channels each; 0.38 s after the arm falls in step 4, which ends at 17 ticks + 4 · 3 · 3.7 ms. The next
        // portrait's blit, then its 21.
        Assert.Equal(arm + 2 * F + 17 / 50.0 + 12 * Ch + Call + 2.4 + F, screen.SoundStarts()[4].At, 6);
    }

    [OriginalDataFact]
    public void SixMembers_LandOnTheS130Capture()
    {
        // s153 v1 (no keys, 60 fps, sound recorded), six OK members, the real Zeus speech; times from the screen clear at
        // 4.467 s of the video (capture_s153_sounds.md); s130's r1 was 0.03-0.06 s off these with its 30 fps.
        var pack = Pack;
        var fx = pack.DosoundEffects!;
        var image = fx.TryLoadImage()!;
        var six = new VictoryScene([.. Enumerable.Range(0, 6).Select(_ => new VictorySlot(Race.Human, true))], Endgame.ZeusSpeech);
        var screen = new VictoryScreen(pack.EndingScene!, six,
            VictorySoundTimes.From(pack.EndingScene!.Sounds!, n => fx.Hold(image, n)));
        // tol: the video and the recorded sound's clocks disagree by 0.2 % (portraits 3.200 s apart on video, 3.207 in the
        // sound), so a mark 30 s on is good to ~0.05 s.
        const double clear = 4.467, tol = 0.05;
        Assert.InRange(screen.FadeInAt, 12.05 - clear - tol, 12.05 - clear + tol);
        Assert.InRange(screen.GhostsAt, 17.117 - clear - tol, 17.117 - clear + tol); // the first ghost
        Assert.InRange(screen.ArmAt, 17.367 - clear - tol, 17.367 - clear + tol);
        Assert.InRange(screen.SpeechAt, 36.583 - clear - tol, 36.583 - clear + tol);
        Assert.InRange(screen.SpeechEnd, 64.163 - clear - tol, 64.163 - clear + tol);
        Assert.InRange(screen.FadeOutAt, 71.02 - clear - tol, 71.02 - clear + tol);
        Assert.InRange(screen.DoneAt, 77.167 - clear - tol, 77.167 - clear + tol);
    }

    [OriginalDataFact]
    public void SoundOff_KeepsTheDrains()
    {
        // With the Sound switch off the waits are 0 but each sound still drains the port (s129 / s130).
        var screen = new VictoryScreen(Pack.EndingScene!, Scene);
        var full = screen.SpeechEnd;
        screen.Key(screen.FadeInAt + 1);
        Assert.Equal(full, screen.SpeechEnd, 6);
    }

    [OriginalDataFact]
    public void Renderer_LastsTheSoundsTicks()
    {
        var fx = Pack.DosoundEffects!;
        var image = fx.TryLoadImage()!;
        var r = SoundEffectPlayer.Render(fx, image, 25, 44100)!;
        Assert.Equal(Math.Round(125 * (0.02 + 2 * Ch) * 44100), r.Length, 1.0); // 125 two-channel one-tick steps (3.425 s)
        var pcm = r.RenderAll();
        Assert.Contains(pcm, v => v != 0);
        Assert.Null(SoundEffectPlayer.Render(fx, image, 33));
    }
}
