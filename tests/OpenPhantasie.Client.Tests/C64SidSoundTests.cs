using System.Text.Json;
using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s226: the C64 end screens' SID sounds (<c>sid_sounds</c> in I's and II's <c>graphics/c64.json</c>,
/// <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.6): each decoded sound renders on the 6581 model, sounds, and
/// lasts as the programs time it; the screens start them at their moments.
/// </summary>
public class C64SidSoundTests
{
    private static SidSounds Load(GameId game)
    {
        var path = Path.Combine(GameDataRepository.DefaultPathFor(game), "graphics", "c64.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return SidSounds.FromJson(doc.RootElement.GetProperty("sid_sounds").GetRawText(), path);
    }

    private static readonly SidSounds I = Load(GameId.P1), II = Load(GameId.P2);

    private static double Rms(float[] stereo, double from, double to, int rate = TuneRenderer.DefaultSampleRate)
    {
        var a = (int)(from * rate);
        var b = Math.Min(stereo.Length / 2, (int)(to * rate));
        double sum = 0;
        for (var i = a; i < b; i++)
            sum += stereo[2 * i] * stereo[2 * i];
        return b > a ? Math.Sqrt(sum / (b - a)) : 0;
    }

    private static float[] Render(SidScore score) => new SidRenderer(score, Sid6581.PalClockHz).RenderAll();

    [Fact]
    public void Packs_HaveTheDecodedSounds()
    {
        Assert.Equal(["click", "click_off", "fate", "rumble", "thunder", "wind", "wind_stop"], I.Names.Order(StringComparer.Ordinal));
        Assert.Equal(["cheer", "fate", "rumble"], II.Names.Order(StringComparer.Ordinal));
        Assert.Equal((985248.0, 60.0), (I.ClockHz, I.JiffyHz));
    }

    [Fact]
    public void Thunder_SoundsAndStopsItsGates_After03Seconds()
    {
        var score = I.Score("thunder", () => 0.5);
        Assert.Equal(0.3, score.Writes[^1].Seconds, 6);
        Assert.Equal((4, 20), (score.Writes[^2].Register, score.Writes[^2].Value));
        Assert.Equal(0.3 + 2, score.Seconds, 6);
        var pcm = Render(score);
        Assert.Equal((int)Math.Ceiling(2.3 * TuneRenderer.DefaultSampleRate), pcm.Length / 2);
        Assert.True(Rms(pcm, 0.05, 0.3) > 0.02);
        // Release 10 (1.5 s, exponential): quieter a second later, near silence at the end.
        Assert.True(Rms(pcm, 1.2, 1.4) < Rms(pcm, 0.05, 0.3));
    }

    [Fact]
    public void Click_RingsVoice2_UntilItsGateGoesOff()
    {
        // P.END 0xc43 after a space: the gate on, 16 jiffies, the gate off (release 4: 114 ms).
        var score = I.Score([new SidCue(0, "click"), new SidCue(16 / 60.0, "click_off")], () => 0.5);
        var lo = score.Writes.Last(w => w.Register == 0);
        Assert.Equal(110 + 35, lo.Value); // 110 + INT(RND * 70)
        var pcm = Render(score);
        Assert.True(Rms(pcm, 0.02, 16 / 60.0) > 0.01);
        Assert.True(Rms(pcm, 16 / 60.0 + 0.6, 16 / 60.0 + 0.8) < 0.001);
    }

    [Fact]
    public void Rumble_GrowsUnderItsLongAttack_AndKeepsSounding()
    {
        // CEMETARY 0x90e: voice 2 noise, attack 15 (8 s), through the resonant low-pass; the gate is never turned off.
        var score = I.Score([new SidCue(0, "rumble"), new SidCue(12, "fate")], () => 0.5);
        Assert.Contains(score.Writes, w => w is { Register: 11, Value: 135 });
        var pcm = Render(score);
        var early = Rms(pcm, 0.5, 1.5);
        var late = Rms(pcm, 9, 11.9);
        Assert.True(late > 0.005, $"late {late}");
        Assert.True(late > 2 * early, $"early {early}, late {late}");
        Assert.Equal(II.Score("rumble", () => 0.5).Writes, I.Score("rumble", () => 0.5).Writes); // CEMETARY2: the same
    }

    [Fact]
    public void Fate_DecaysToSilence_AndItsGateGoesOffAtTL108()
    {
        var score = I.Score("fate", () => 0.5);
        Assert.Equal(1.08, score.Writes[^1].Seconds, 6);
        Assert.Equal(25 + 8 + 1, score.Writes.Count); // the 25-register clear, 8 pokes, the gate off
        var pcm = Render(score);
        Assert.True(Rms(pcm, 0.0, 0.3) > 0.02);
        Assert.True(Rms(pcm, 1.5, 2.0) < Rms(pcm, 0.0, 0.3) / 10);
    }

    [Fact]
    public void Cheer_Sweeps_Pauses_Sweeps_AndFades_In9928Seconds()
    {
        var score = II.Score("cheer", () => 0.5);
        var freq = score.Writes.Where(w => w.Register == 1).ToList();
        Assert.Equal(1 + 58, freq.Count); // the clear, 29 passes down, 29 up
        var volumes = score.Writes.Where(w => w.Register == 24).Select(w => w.Value).ToList();
        Assert.Equal([0, 79, .. Enumerable.Range(0, 16).Select(v => 15 - v)], volumes);
        // The last fade step's FOR 500 ends the sound at the measured 9.928 s.
        var last = score.Writes.Last(w => w.Register == 24).Seconds;
        Assert.Equal(9.928, last + 501 * 4.171 / 4001, 3);
        var pcm = Render(score);
        // Attack 14 (5 s): it starts quiet and grows while the volume falls from 1.57 s.
        Assert.True(Rms(pcm, 0.1, 0.5) > 0.003);
        Assert.True(Rms(pcm, 1.0, 1.5) > Rms(pcm, 0.1, 0.5));
        Assert.True(Rms(pcm, 1.6, 2.0) > Rms(pcm, 8.5, 9.3)); // the fade
    }

    [Fact]
    public void Wind_RisesAndFallsBetween100And200_FromJiffy2_UntilItsStop()
    {
        var random = new Random(7);
        var score = I.Score([new SidCue(0, "wind", 20), new SidCue(20, "wind_stop")], random.NextDouble);
        var gatesOn = score.Writes.Where(w => w is { Register: 4, Value: 0x81 }).ToList();
        Assert.NotEmpty(gatesOn);
        Assert.Equal(2 / 60.0, gatesOn[0].Seconds, 6); // $4E64 = 2 at the start
        var pitches = score.Writes.Where(w => w.Register == 1 && w.Seconds < 20).Select(w => w.Value).Skip(1).ToList();
        Assert.All(pitches, p => Assert.InRange(p, 100, 200));
        Assert.Contains(200, pitches);
        Assert.All(score.Writes.Where(w => w.Seconds < 20 && w.Seconds > 0), w => Assert.Contains(w.Register, new[] { 1, 4, 8, 11 }));
        Assert.All(score.Writes.Where(w => w.Seconds >= 20), w => Assert.Equal(0, w.Value)); // the stop clears all 29
        var pcm = Render(score);
        Assert.True(Rms(pcm, 0, 20) > 0.003);
        Assert.True(Rms(pcm, 20.1, 22) < 0.001);
    }

    [Fact]
    public void WindRoutine_Silence_Is31To158Jiffies_AfterAGust()
    {
        var p = new SidSounds.WindParams(100, 200, 2, 1, 8, 4, 11, 0x81, 0x80, 0x11, 0x10);
        // rnd = 0: one rise and fall per gust (R = 1 + 0... the fall counter compares with R = 1), period 1, pause 1.
        var writes = SidSounds.WindWrites(p, 2000, () => 0).ToList();
        var on = writes.Where(w => w is { Register: 4, Value: 0x81 }).Select(w => w.Jiffy).ToList();
        // A gust with every random 0: 100 rises, 1 pause, 100 falls, R = 1 so a second rise and fall, then 31 jiffies.
        Assert.Equal(2, on[0]);
        Assert.Equal(on[0] + 100 + 1 + 100 + 100 + 1 + 100 + 31, on[1]);
        var top = SidSounds.WindWrites(p, 2000, () => 0.999).ToList();
        var onTop = top.Where(w => w is { Register: 4, Value: 0x81 }).Select(w => w.Jiffy).ToList();
        // Every random 255: period 4, pause 8, fall period 2, R = 2 (three passes), 127 + 31 = 158 jiffies of silence.
        Assert.Equal(onTop[0] + 3 * (400 + 8 + 200) + 158, onTop[1]);
    }

    private static C64EndingLayout Ending() => new()
    {
        VblHz = 50, JiffyHz = 60,
        Astral = new C64TextLine("a", 3, 23, 7, 2),
        Cloud = "ending.ghost", CloudLeftX = 66, CloudRightX = 216, CloudGap = 30, CloudFromY = 190, CloudToY = 100,
        CloudStepSeconds = 0.05, AfterRiseSeconds = 2, WipeX = 104, WipeY = 16, WipeWidth = 96, WipeHeight = 104,
        WipeFrames = 7, AfterWipeSeconds = 2, StrikeHand = "ending.arm", ThunderSeconds = 1.79, FigureSeconds = 0.3,
        AfterStrikeSeconds = 0.7, Smiles = new C64TextLine("s", 8, 24, 7, 3), Face = ["f"], FaceBlank = [0, 0, 1, 1],
        SpeechCol = 1, SpeechRow = 18, SpeechColour = 7, SpaceJiffies = 16, RandomJiffies = 8, Hand = ["h"],
        HandBlank = [0, 0, 1, 1], GrantSeconds = 12,
    };

    [Fact]
    public void Ending_CuesTheWind_AThunderPerSlot_AndAClickPerSpaceAndLine()
    {
        var scene = new VictoryScene([new VictorySlot(Race.Human, true), new VictorySlot(Race.Elf, false)], ["A B", "C"]);
        var screen = new C64EndingScreen(Ending(), scene, ["A B", "C"], "x", "y", () => 0.5);
        var cues = screen.SoundCues();
        Assert.Equal(new SidCue(screen.RiseAt, "wind", screen.StrikesAt), cues[0]);
        Assert.Equal(new SidCue(screen.StrikesAt, "wind_stop"), cues[1]);
        Assert.Equal([screen.StrikesAt, screen.StrikesAt + 2.79], cues.Where(c => c.Sound == "thunder").Select(c => c.At));
        // One space and two line ends: three clicks, each gate off 16 jiffies (a space) or the last RND * 8 (a line end) later.
        var on = cues.Where(c => c.Sound == "click").Select(c => c.At).ToList();
        var off = cues.Where(c => c.Sound == "click_off").Select(c => c.At).ToList();
        Assert.Equal(3, on.Count);
        Assert.Equal(16 / 60.0, off[0] - on[0], 6);
        Assert.Equal(4 / 60.0, off[1] - on[1], 6);
        Assert.InRange(on[0], screen.SpeechAt, screen.GrantAt);
        Assert.True(I.Score(cues, () => 0.5).Seconds > screen.GrantAt - 0.5);
    }

    [Fact]
    public void Defeat_CuesTheRumbleAfterTheGhosts_AndAFateSoundPerMember()
    {
        var layout = new C64DefeatLayout
        {
            Temple = "t", Ghost = "g", Door = "d", Face = "f", Defeated = new C64TextLine("x", 6, 23, 2, 2), TextCol = 1,
            TextRow = 23, RowX2 = 172, RowGap = 35, RowY = 120, GhostSeconds = 0.33, AfterGhostsSeconds = 2, DoorX = 124,
            DoorY = 27, DoorSteps = 78, DoorStepSeconds = 0.1, FaceX = 124, FaceY = 31, DemonSeconds = 4,
            FateSoundSeconds = 1.17, FateCopySeconds = 4.1, FateSeconds = 2, EndSeconds = 6,
        };
        var defeat = new PartyDefeatResult
        {
            Fates = [(new Character { Name = "A" }, DefeatFate.Resurrected), (new Character { Name = "B" }, DefeatFate.Destroyed)],
        };
        var screen = new C64DefeatScreen(layout, defeat, ["1", "2", "3", "4"], "X");
        var cues = screen.SoundCues();
        Assert.Equal(new SidCue(2 + 2 * 0.33, "rumble"), cues[0]);
        Assert.Equal(screen.FatesAt, cues[1].At, 6);
        Assert.Equal(screen.FatesAt + 1.17 + 4.1 + 2, cues[2].At, 6);
        Assert.All(cues.Skip(1), c => Assert.Equal("fate", c.Sound));
    }
}
