using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// IV's picture animations (s109, T6.4; <c>FORMAT.md</c> §6.10): the X68000 pack's <c>picture_animations</c> tables
/// (P4.X <c>0x45764</c> frames, <c>0x46174</c> records, <c>_setanim</c> call sites; <c>tools/scripts/x68k_p4_anim.py
/// --check</c> pins them to the binary), the sheets they cut, and <see cref="PictureAnimator"/> (<c>_setanim</c> /
/// <c>_anima</c>) on synthetic passes, and <see cref="PictureAnimationPlayer"/> (s110: real time at the pass lengths
/// measured in XEiJ, per input loop).
/// </summary>
public class P4PictureAnimationTests
{
    private static PictureAnimationSet Set() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!.PictureAnimations!;

    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;

    /// <summary>Always rolls <c>value</c> (clamped into the asked range).</summary>
    private sealed class FixedRoll(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    [OriginalDataFact]
    public void Tables_HaveP4XsCounts_AndTheMeasuredPassLengths()
    {
        var s = Set();
        Assert.Equal(161, s.Table.Frames.Count);
        Assert.Equal(45, s.Table.Records.Count);
        Assert.Equal(6.1, s.PassMilliseconds); // the building loops' value: only a fallback, every scene has its own
        Assert.Equal(5.83, s.PassMillisecondsFor(s.Scenes["title"]));
        Assert.Equal(6.69, s.PassMillisecondsFor(s.Scenes["p4.street.0"]));
        Assert.Equal(6.38, s.PassMillisecondsFor(s.Scenes["p4.street.1"]));
        Assert.Equal(6.8, s.PassMillisecondsFor(s.Scenes["p4.street.2"]));
        Assert.Equal(6.04, s.PassMillisecondsFor(s.Scenes["p4.guild"]));
        Assert.Equal(6.06, s.PassMillisecondsFor(s.Scenes["p4.inn"]));
        Assert.Equal(5.95, s.PassMillisecondsFor(s.Scenes["p4.bank"]));
        Assert.Equal(6.12, s.PassMillisecondsFor(s.Scenes["p4.mystic"]));
        Assert.Equal(1.359, s.PassMillisecondsFor(s.Scenes["p4.mystic.reading"])); // the bare _anima loops
        // s117 / s118, XEiJ (mean interval between _anima calls); s117: the Shipwright's yes / no offer, the three ending waits
        Assert.Equal(6.06, s.PassMillisecondsFor(s.Scenes["p4.shipwright"]));
        Assert.Equal(7.39, s.PassMillisecondsFor(s.Scenes["p4.ending.street"]));
        Assert.Equal(5.64, s.PassMillisecondsFor(s.Scenes["p4.ending.banquet"]));
        Assert.Equal(5.53, s.PassMillisecondsFor(s.Scenes["p4.ending.cell"]));
        Assert.All(s.Scenes.Values, sc => Assert.NotNull(sc.PassMilliseconds));
        Assert.Equal(790, s.DoorHoldMilliseconds);
        Assert.All(s.Scenes.Values, sc => Assert.InRange(s.PassMillisecondsFor(sc)!.Value, 1, 10));
    }

    [OriginalDataFact]
    public void TitleTorches_AreRecords27To29_OnOpenAni()
    {
        var s = Set();
        var title = s.Scenes["title"];
        Assert.Equal(("title.animation", 27, 29), (title.Sheet, title.First, title.Last));
        var left = s.Table.Records[27];
        Assert.Equal((0, 60, 24, 31), (left.X, left.Y, left.Width, left.Height));
        Assert.Equal([112, 113, 114], left.Frames);
        Assert.Equal(new PictureAnimationFrame(0, 0, 100, 50), s.Table.Frames[112]);
        Assert.Equal(new PictureAnimationFrame(48, 62, 300, 300), s.Table.Frames[120]);
        Assert.Equal([115, 116, 117], s.Table.Records[28].Frames);
        Assert.Equal(280, s.Table.Records[28].X);
    }

    [OriginalDataTheory]
    [InlineData("p4.street.0", "p4.anim.street.1", 0, 3)]
    [InlineData("p4.street.1", "p4.anim.street.2", 4, 7)]
    [InlineData("p4.street.2", "p4.anim.street.3", 8, 14)]
    [InlineData("p4.guild", "p4.anim.guild", 33, 35)]
    [InlineData("p4.inn", "p4.anim.inn", 20, 23)]
    [InlineData("p4.bank", "p4.anim.bank", 24, 26)]
    [InlineData("p4.shipwright", "p4.anim.shipwright", 30, 32)]
    [InlineData("p4.mystic", "p4.anim.mystic", 15, 18)]
    [InlineData("p4.ending.street", "p4.anim.ending.1", 36, 42)]
    [InlineData("p4.ending.banquet", "p4.anim.ending.2", 43, 43)]
    [InlineData("p4.ending.cell", "p4.anim.ending.3", 44, 44)]
    public void EachPicture_ArmsItsSetanimRecords(string scene, string sheet, int first, int last)
    {
        var s = Set().Scenes[scene];
        Assert.Equal((sheet, first, last), (s.Sheet, s.First, s.Last));
        if (!scene.StartsWith("p4.ending.street", StringComparison.Ordinal))
            Assert.Equal((0, 0), (s.X, s.Y));
        else
            Assert.Equal((0, 134), (s.X, s.Y)); // END1 at (160, 154) under the street at (160, 20)
    }

    [OriginalDataFact]
    public void EveryFrame_ASceneUses_LiesInsideItsSheet_AndEverySpotInsideTheScreen()
    {
        var pack = Pack();
        var s = pack.PictureAnimations!;
        foreach (var (name, scene) in s.Scenes)
        {
            var path = pack.Resolve(scene.Sheet);
            Assert.True(path is not null, $"{name}: sheet {scene.Sheet} missing");
            var (w, h) = PngSize(path!);
            for (int n = scene.First; n <= scene.Last; n++)
            {
                var r = s.Table.Records[n];
                Assert.True(r.X % 8 == 0 && r.Width % 8 == 0, $"record {n}: _ggcopy works in 8-pixel columns");
                Assert.InRange(scene.X + r.X + r.Width, 1, pack.Screen.Width);
                foreach (var f in r.Frames)
                {
                    var fr = s.Table.Frames[f];
                    Assert.True(fr.SheetX + r.Width <= w && fr.SheetY + r.Height <= h,
                        $"{name}: frame {f} ({fr.SheetX}, {fr.SheetY}) + {r.Width} x {r.Height} outside {w} x {h}");
                }
            }
        }
    }

    [OriginalDataFact]
    public void Doors_AreTheStreetSheetsRightStrip()
    {
        var s = Set();
        Assert.Equal((40, 80), (s.DoorWidth, s.DoorHeight));
        Assert.Equal(3, s.DoorsByTownType.Count);
        Assert.Equal(new PictureDoor(8, 235, 144, 0), s.DoorsByTownType[0][0]);
        Assert.Null(s.DoorsByTownType[0][6]);
        Assert.Null(s.DoorsByTownType[2][5]);
    }

    [OriginalDataFact]
    public void Arm_WaitsTheNextFramesTime_AndShowsNothing()
    {
        var a = new PictureAnimator(Set().Table, new FixedRoll(50));
        a.Arm(27, 29);
        // frame 112: (rand(50) + 100) >> 2 with rand = 50 -> 37; frame 118: (50 + 300) >> 2 = 87.
        Assert.Equal(37, a.Countdown(27));
        Assert.Equal(87, a.Countdown(29));
        Assert.Equal(0, a.Position(27));
        Assert.False(a.IsArmed(26));
    }

    [Fact]
    public void Pass_FiresWhenTheCountWasZero_ThenStepsAndWraps()
    {
        var table = new PictureAnimationTable(
            [new(0, 0, 8, 1), new(24, 0, 4, 1)],
            [new(0, 0, 24, 31, [0, 1])]);
        var a = new PictureAnimator(table, new FixedRoll(1));
        a.Arm(0, 0);                       // frame 0: (1 + 8) >> 2 = 2
        Assert.Empty(a.Pass());            // 2 -> 1
        Assert.Empty(a.Pass());            // 1 -> 0
        Assert.Equal([new PictureFrameCopy(0, 0)], a.Pass()); // was 0: show frame 0, wait (1 + 8) >> 2 = 2
        Assert.Equal(1, a.Position(0));
        Assert.Empty(a.Pass());
        Assert.Empty(a.Pass());
        Assert.Equal([new PictureFrameCopy(0, 1)], a.Pass()); // frame 1, wait (1 + 4) >> 2 = 1
        Assert.Equal(0, a.Position(0));    // -1 wraps to the start
        Assert.Empty(a.Pass());
        Assert.Equal([new PictureFrameCopy(0, 0)], a.Pass());
    }

    [Fact]
    public void Clear_StopsTheRecords_ButKeepsTheirPositions()
    {
        var table = new PictureAnimationTable([new(0, 0, 0, 1), new(0, 0, 0, 1)], [new(0, 0, 8, 8, [0, 1])]);
        var a = new PictureAnimator(table, new FixedRoll(1)); // every wait (1 + 0) >> 2 = 0
        a.Arm(0, 0);
        Assert.Single(a.Pass());
        a.Clear();
        Assert.Empty(a.Pass());
        a.Arm(0, 0);
        Assert.Equal([new PictureFrameCopy(0, 1)], a.Pass()); // the position (+16) is never reset
    }

    private static (int W, int H) PngSize(string path)
    {
        var b = new byte[24];
        using (var f = File.OpenRead(path))
            f.ReadExactly(b);
        return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
    }

    private static readonly PictureAnimationTable TwoFrames = new(
        [new(0, 0, 8, 1), new(24, 0, 4, 1)],
        [new(0, 0, 24, 31, [0, 1]), new(24, 0, 24, 31, [1, 0])]);

    [Fact]
    public void Player_TurnsTimeIntoPasses_KeepingTheRemainder()
    {
        var p = new PictureAnimationPlayer(TwoFrames, new FixedRoll(1));
        p.Show("a", 0, 0, 5.0);                 // frame 0 waits (1 + 8) >> 2 = 2: it shows on pass 3
        Assert.Equal(1, p.Advance(7.5));        // 1 pass, 2.5 ms carried
        Assert.Empty(p.Shown);
        Assert.Equal(2, p.Advance(7.5));        // 10 ms: passes 2 and 3
        Assert.Equal(new Dictionary<int, int> { [0] = 0 }, p.Shown);
        Assert.Equal(0, p.Advance(4.9));
        Assert.Equal(0, p.Advance(-3));
        Assert.Equal(PictureAnimationPlayer.MaxPassesPerAdvance, p.Advance(1e9)); // a stall does not replay hours
    }

    [Fact]
    public void Player_SamePicture_KeepsGoing_AnotherOne_StartsPlain_ButRecordsKeepTheirPlace()
    {
        var p = new PictureAnimationPlayer(TwoFrames, new FixedRoll(1));
        p.Show("street", 0, 0, 1.0);
        p.Advance(3);                             // frame 0 shown, position 1
        Assert.Equal(0, p.Shown[0]);
        p.Show("street", 0, 0, 1.0);              // the same picture: nothing re-armed
        Assert.Equal(0, p.Shown[0]);
        p.Show("guild", 1, 1, 2.0);               // _clanim + _setanim: drawn plain
        Assert.Empty(p.Shown);
        Assert.False(p.Animator.IsArmed(0));
        Assert.Equal(2.0, p.PassMilliseconds);
        p.Hide();
        Assert.Null(p.Scene);
        Assert.Equal(0, p.Advance(100));
        p.Show("street", 0, 0, 1.0);
        p.Advance(2);                             // frame 1's wait (1 + 4) >> 2 = 1: shows on pass 2
        Assert.Equal(1, p.Shown[0]);              // carries on at position 1 (+16 is never reset)
    }

    [Fact]
    public void Player_RejectsABadPassLength()
    {
        var p = new PictureAnimationPlayer(TwoFrames, new FixedRoll(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Show("a", 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Show("a", 0, 0, double.NaN));
    }

    [Fact]
    public void PassLengths_FromThePack_TheLoaderRejectsNonPositiveOnes()
    {
        var dir = Directory.CreateTempSubdirectory("p4anim");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "graphics"));
            File.WriteAllBytes(Path.Combine(dir.FullName, "s.png"), [137, 80, 78, 71]);
            File.WriteAllText(Path.Combine(dir.FullName, "graphics", "index.json"), """{"packs": ["pack.json"]}""");
            var root = dir.FullName.Replace("\\", "/");
            void Write(string scenePass, string doorHold) => File.WriteAllText(Path.Combine(dir.FullName, "graphics", "pack.json"),
                $$$"""
                {"id": "t", "game": "phantasie4", "platform": "x", "name": "t", "root": "{{{root}}}",
                 "pictures": {"sheet": "s.png"},
                 "picture_animations": {"pass_ms": 6.1, "frames": [[0, 0, 100, 50]],
                   "records": [{"at": [0, 0], "size": [8, 8], "frames": [0]}],
                   "scenes": {"x": {"sheet": "sheet", "at": [0, 0], "records": [0, 0]{{{scenePass}}}}},
                   "doors": {"size": [40, 80]{{{doorHold}}}, "by_town_type": []}} }
                """);
            Write(", \"pass_ms\": 5.5", ", \"hold_ms\": 790");
            var set = GraphicsPackCatalog.Load(dir.FullName).Available.Single().PictureAnimations!;
            Assert.Equal(5.5, set.PassMillisecondsFor(set.Scenes["x"]));
            Assert.Equal(790, set.DoorHoldMilliseconds);
            Write(", \"pass_ms\": 0", "");
            Assert.Throws<InvalidDataException>(() => GraphicsPackCatalog.Load(dir.FullName));
            Write("", ", \"hold_ms\": -1");
            Assert.Throws<InvalidDataException>(() => GraphicsPackCatalog.Load(dir.FullName));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    // --- The Mystic's reading (_mystic 0x6BF0 / 0x6F78, s115) ---------------------------------------------------------

    private static P4MysticReading Mystic(out PictureAnimationPlayer player)
    {
        player = new PictureAnimationPlayer(Set().Table, new FixedRoll(1));
        player.Show("p4.street.0", 0, 3, 6.64); // the street before: the Mystic is a new picture
        return new P4MysticReading(player, "p4.mystic", 1.435, 6.12);
    }

    [OriginalDataFact]
    public void MysticScenes_AreRecords15To18_AndTheReadingRecord19()
    {
        var s = Set();
        Assert.Equal((15, 18), (s.Scenes["p4.mystic"].First, s.Scenes["p4.mystic"].Last));
        Assert.Equal((19, 19), (s.Scenes["p4.mystic.reading"].First, s.Scenes["p4.mystic.reading"].Last));
        Assert.Equal((P4MysticReading.First, P4MysticReading.Last, P4MysticReading.ReadingRecord), (15, 18, 19));
        // The bubble is not part of the plain picture: it comes after the reading.
        Assert.DoesNotContain(Pack().Scenes["p4.mystic"], part => part.Picture == "p4.building.mystic.bubble");
    }

    [OriginalDataFact]
    public void MysticReading_RunsTheThreeCountedLoops_ThenWaits()
    {
        var m = Mystic(out var player);
        Assert.Equal("p4.mystic", player.Scene);
        Assert.Equal(P4MysticPhase.Gazing, m.Phase);
        Assert.False(m.AcceptsKeys);
        Assert.False(m.BubbleUp);
        Assert.True(player.Animator.IsArmed(15) && player.Animator.IsArmed(18));
        Assert.False(player.Animator.IsArmed(19));
        Assert.False(player.Animator.IsArmed(0)); // the street's records were disarmed

        m.Advance(9999 * 1.435 + 0.5);
        Assert.Equal((P4MysticPhase.Gazing, 1), (m.Phase, m.PassesLeft));
        m.Advance(1.435);
        Assert.Equal((P4MysticPhase.Reading, 3000), (m.Phase, m.PassesLeft));
        Assert.True(player.Animator.IsArmed(19));
        Assert.True(player.Animator.IsArmed(15)); // no _clanim before record 19: 15–18 keep running

        m.Advance(3000 * 1.435);
        Assert.Equal(P4MysticPhase.Speaking, m.Phase);
        Assert.True(m.BubbleUp);
        Assert.False(m.AcceptsKeys);

        m.Advance(3000 * 1.435);
        Assert.Equal(P4MysticPhase.Waiting, m.Phase);
        Assert.True(m.AcceptsKeys);
        Assert.False(player.Animator.IsArmed(19)); // _clanim + _setanim 15–18 before _spc_wait
        Assert.True(player.Animator.IsArmed(15));
        Assert.Equal(6.12, player.PassMilliseconds);
    }

    [OriginalDataFact]
    public void MysticReading_TakesAbout23Seconds_AndKeepsRecord19sLastFrame()
    {
        var m = Mystic(out var player);
        m.Advance(14000);
        Assert.Equal(P4MysticPhase.Gazing, m.Phase); // 10 000 × 1.435 ms = 14.35 s
        m.Advance(400);
        Assert.Equal(P4MysticPhase.Reading, m.Phase);
        m.Advance(4300);
        Assert.Equal(P4MysticPhase.Speaking, m.Phase); // + 4.305 s
        Assert.True(player.Shown.ContainsKey(19)); // record 19 copied a frame during the reading
        m.Advance(5000); // past the third loop into the wait
        Assert.Equal(P4MysticPhase.Waiting, m.Phase);
        var shown = player.Shown[19];
        m.Advance(60000);
        Assert.Equal(shown, player.Shown[19]);
        Assert.True(player.Shown.ContainsKey(19)); // disarmed, but its frame stays on the picture (no redraw)
    }

    [OriginalDataFact]
    public void MysticSkip_EndsTheLoopsAtOnce_WithThePictureTheWaitedReadingEndsOn()
    {
        // QoL MYSTIC SKIP: a key in any loop runs the passes left at once; the picture is the one the full ~23 s give.
        var waited = Mystic(out var waitedPlayer);
        waited.Advance((P4MysticReading.GazePasses + P4MysticReading.ReadPasses + P4MysticReading.SpeakPasses) * 1.435 + 1.0);
        Assert.Equal(P4MysticPhase.Waiting, waited.Phase);

        foreach (var at in new[] { 0.0, 5000.0, 15000.0, 20000.0 }) // gazing, gazing, reading, speaking
        {
            var skipped = Mystic(out var skippedPlayer);
            skipped.Advance(at);
            Assert.True(skipped.SkipLoops());
            Assert.Equal(P4MysticPhase.Waiting, skipped.Phase);
            Assert.True(skipped.AcceptsKeys && skipped.BubbleUp);
            Assert.Equal(0, skipped.PassesLeft);
            Assert.Equal(waitedPlayer.Shown.OrderBy(kv => kv.Key), skippedPlayer.Shown.OrderBy(kv => kv.Key));
            for (var record = P4MysticReading.First; record <= P4MysticReading.ReadingRecord; record++)
                Assert.Equal(waitedPlayer.Animator.IsArmed(record), skippedPlayer.Animator.IsArmed(record));
            Assert.Equal(waitedPlayer.PassMilliseconds, skippedPlayer.PassMilliseconds);
            Assert.False(skipped.SkipLoops()); // nothing left: the wait is _spc_wait's, a key there closes the Mystic
        }
    }

    [Fact]
    public void PlayerArm_AddsRecords_OrClearsFirst_AndKeepsTheScreen()
    {
        var p = new PictureAnimationPlayer(TwoFrames, new FixedRoll(1));
        Assert.Throws<InvalidOperationException>(() => p.Arm(0, 0, 5));
        p.Show("x", 0, 0, 5);
        p.Run(100);
        Assert.NotEmpty(p.Shown);
        p.Arm(0, 0, 2, clearFirst: true);
        Assert.Equal(2, p.PassMilliseconds);
        Assert.NotEmpty(p.Shown);
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Arm(0, 0, 0));
    }
}
