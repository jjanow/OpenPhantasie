using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// I Amiga's victory screen (h32 <c>+0x6d0</c>, s127 static): the pen-by-pen fade, the 2.4 s hold, the ghosts and Zeus'
/// arm, one portrait per granted member 2.4 s apart, the speech line by line (180 ticks or a key), the fade-out.
/// </summary>
public partial class VictoryScreenTests
{
    private static EndingSceneLayout Layout() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.EndingScene!;

    private static readonly VictoryScene Scene = new(
        [new(Race.Human, true), new(Race.Elf, false), new(Race.Dwarf, true)], ["one", "two", "three"]);

    [OriginalDataFact]
    public void Pack_HasTheCodesNumbers()
    {
        var layout = Layout();
        Assert.Equal((60.0, 10, 120, 120, 180), (layout.VblHz, layout.FadeVbl, layout.HoldTicks, layout.GrantTicks, layout.LineTicks));
        Assert.Equal((160, 48, 158, 192, 5), (layout.ArmX, layout.ArmY, layout.RowTop, layout.TextY, layout.TextPen));
        Assert.Equal("#EEEE00", layout.Palette[5]); // combat table h69 +0x94 pen 5 (0xEE0 × 17)
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;
        Assert.All(new[] { PictureNames.EndingFinal, layout.Ghost!, layout.Arm! }, p => Assert.True(File.Exists(pack.Resolve(p))));
    }

    /// <summary>One line: 180 polls of Delay(1) + a port check, 21.87 ms each (s130 live).</summary>
    private const double Line = 180 * 0.02187;

    private const double Fade = 16 * 10 / 60.0;

    [OriginalDataFact]
    public void Pack_HasTheLiveTimes()
    {
        var layout = Layout();
        Assert.Equal(0.02187, layout.LinePollSeconds);
        Assert.True(layout.QueuedKeys);
        Assert.Equal(new EndingDiskWaits(3.5, 6.09, 1, 3.47), layout.Disk); // s153 refit
        Assert.Equal(7, layout.SpeechKeys!.Count);
    }

    [OriginalDataFact]
    public void Timeline_DiskFadeHoldGrantsSpeechSaveFadeTown()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        // final.pic read 3.50 s on black (no sound times: the sound waits are 0), then 16 pens × 10 VBLs at 60 Hz.
        Assert.Equal(3.5, screen.FadeInAt, 6);
        Assert.Equal(0, screen.At(3.4).LitPens);
        var f = screen.FadeInAt;
        Assert.Equal(f + Fade + 2.4, screen.GhostsAt, 6);
        const double F = 1 / 60.0; // s153: each blit waits a WaitTOF (two ghosts, the arm, then each portrait)
        Assert.Equal(screen.GhostsAt + 3 * F + 2 * (F + 2.4), screen.SpeechAt, 6); // two granted members
        Assert.Equal(screen.SpeechAt + 3 * Line, screen.SpeechEnd, 6);  // three lines of 180 polls
        Assert.Equal(screen.SpeechEnd + 6.09, screen.FadeOutAt, 6);     // guild.dat written

        Assert.Equal(1, screen.At(f).LitPens);            // pen 0 set at once
        Assert.Equal(2, screen.At(f + 10 / 60.0).LitPens);
        Assert.Equal(16, screen.At(f + 2.6).LitPens);
        Assert.False(screen.At(screen.GhostsAt - 0.01).Ghosts);
        Assert.Equal((true, 0, 1), (screen.At(screen.GhostsAt).Ghosts, screen.At(screen.GhostsAt).Granted,
            screen.At(screen.GhostsAt).GhostCount));
        Assert.Equal(1, screen.At(screen.GhostsAt + 3 * F).Granted);
        Assert.Equal(2, screen.At(screen.GhostsAt + 3 * F + F + 2.4).Granted);
        Assert.Equal(-1, screen.At(screen.SpeechAt - 0.01).Line);
        Assert.Equal(0, screen.At(screen.SpeechAt).Line);
        Assert.Equal(2, screen.At(screen.FadeOutAt - 0.01).Line);

        var fading = screen.At(screen.FadeOutAt + 10 / 60.0);
        Assert.Equal(2, fading.DarkPens);                  // pens 15 and 14 black again
        Assert.True(fading.Shows(13) && !fading.Shows(14));
        Assert.Equal(2, fading.Line);                       // the last line stays while it fades
        var black = screen.At(screen.FadeOutAt + Fade + 1);
        Assert.Equal((16, false), (black.DarkPens, black.Done)); // the town reloads on black
        Assert.Equal(screen.FadeOutAt + Fade + 3.47, screen.DoneAt, 6);
        Assert.True(screen.At(screen.DoneAt).Done);
    }

    [Fact]
    public void Ghosts_OneAFrame_DoubleUpBehindADeadMember()
    {
        // h32 +0x43e(0) blits the ghost into slots 0…n−1 (n = OK members), one WaitTOF each (s153 live); a slot before an
        // OK member stands on that member's place, so with a dead first member slots 0 and 1 share a place and the last
        // place has no ghost until its portrait.
        VictorySlot[] slots = [new(Race.Human, false), new(Race.Elf, true), new(Race.Dwarf, true)];
        var xs = VictoryScreen.SlotXs(slots);
        Assert.Equal(xs[0], xs[1]);
        Assert.Equal(new[] { (0, false) }, VictoryScreen.Drawn(slots, true, 0, ghostCount: 1));
        var two = VictoryScreen.Drawn(slots, true, 0, ghostCount: 2).Select(d => xs[d.Slot]).ToList();
        Assert.Equal(new[] { xs[1], xs[1] }, two);
        Assert.DoesNotContain(xs[2], two);
    }

    [OriginalDataFact]
    public void Key_InALine_PressEndsIt_ReleaseEndsTheNext()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        var at = screen.SpeechAt;
        screen.Key(at + 1);
        Assert.Equal(1, screen.At(at + 1).Line);
        screen.Release(at + 1.17);                          // r2: the next line ends after the key's hold
        Assert.Equal(2, screen.At(at + 1.17).Line);
        Assert.Equal(at + 1.17 + Line, screen.SpeechEnd, 6);
    }

    [OriginalDataFact]
    public void Key_QueuedInTheLastDelay_EndsLines0And1()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        var at = screen.SpeechAt;
        screen.Key(at - 1);                                 // r5: in the Delay after the last portrait, no sound after it
        screen.Release(at - 0.9);
        Assert.Equal(1, screen.At(at + 0.022).Line);       // line 0 ends at its first poll
        Assert.Equal(2, screen.At(at + 0.044).Line);       // line 1 with the release
        Assert.Equal(at + 2 * 0.02187 + Line, screen.SpeechEnd, 6); // line 2 runs full
    }

    [OriginalDataFact]
    public void Key_BeforeASound_IsDrained()
    {
        var screen = new VictoryScreen(Layout(), Scene);
        var full = screen.SpeechEnd;
        screen.Key(1.0);                                    // during the disk read: sound 25's drain drops it
        screen.Key(screen.FadeInAt + 1);                    // r3: in the fade: the arm sound's drain drops it
        screen.Release(screen.GhostsAt - 0.1);
        screen.Key(screen.GhostsAt + 1);                    // in the first portrait's Delay: the next 21 drops it
        Assert.Equal(full, screen.SpeechEnd, 6);
    }

    [Fact]
    public void Slots_SpacedByGrantedCount_TheCodesQuirks()
    {
        // n = 2: gap (320 − 64) / 3 = 85; the Elf (not granted) takes the place the Dwarf will take.
        Assert.Equal([85, 202, 202], VictoryScreen.SlotXs(Scene.Slots));
        // Ghosts in slots 0..n−1 (0x43e(0)), whoever stands there; portraits on the granted members.
        Assert.Equal([(0, false), (1, false), (0, true), (2, true)], VictoryScreen.Drawn(Scene.Slots, true, 2));
        Assert.Empty(VictoryScreen.Drawn(Scene.Slots, false, 0));
    }
}
