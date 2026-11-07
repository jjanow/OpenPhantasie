using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// III Amiga's combat field (s117, FORMAT.md §8): one line at a time, 30 ticks a line and a swing's 21-tick attack frame
/// first (× Speed: Slow ×2, Fast ÷4), sounds 15 / 18 as a swing's line prints, the dead shown from their "dies" line, and
/// the field's places from the code.
/// </summary>
public class CombatFieldRunTests
{
    private static readonly Character A = new() { Name = "A" };
    private static readonly Character B = new() { Name = "B" };
    private static readonly Monster Orc1 = new() { Id = 1, Name = "Orc" };
    private static readonly Monster Orc2 = new() { Id = 1, Name = "Orc" };

    private static CombatFieldRun Run(AmigaCombatSpeed speed = AmigaCombatSpeed.Normal, IEnumerable<Character>? deadBefore = null)
    {
        // A swings and hits Orc 2 (Orc 1 untouched), which dies; then Orc 1 swings and misses.
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "Orc", 9),
            GameText.F("core.combat.x_dies", "Orc"), "", GameText.F("core.combat.x_misses", "Orc"),
        ];
        CombatLineTag[] tags =
        [
            new(0, A, null), new(0, A, null, CombatSwing.Hit), new(4, null, Orc1), new(4, null, Orc1, CombatSwing.Miss),
        ];
        return CombatFieldRun.Plan(log, tags, ["VICTORY"], [A, B], deadBefore ?? [], [Orc1, Orc2],
            m => ReferenceEquals(m, Orc2), speed);
    }

    [Fact]
    public void Lines_ComeOneAtATime_TheSwingAfterItsAttackFrame()
    {
        var run = Run();
        Assert.Equal(5, run.Lines.Count); // the blank line is skipped, the line noted after the round comes last
        var hit = run.Lines[0];
        Assert.Equal((0.0, 420.0, 600.0), (hit.StartMs, hit.AttackMs, hit.ShowMs)); // 21 and 30 ticks of 1/50 s
        Assert.Equal(1020, run.Lines[1].StartMs);
        Assert.Equal(0, run.Lines[1].AttackMs);
        Assert.True(run.Lines[0].Party && run.Lines[2].Party && !run.Lines[3].Party);
        Assert.False(run.Lines[4].Party); // after the round: the last actor's side [~]
        Assert.Equal(1020 + 600 + 600 + 420 + 600 + 600, run.LengthMs);

        var swinging = run.At(100);
        Assert.Same(A, swinging.AttackingMember);
        Assert.False(swinging.TextShown);
        var printed = run.At(500);
        Assert.Null(printed.AttackingMember);
        Assert.True(printed.TextShown);
        Assert.Same(Orc1, run.At(2300).AttackingMonster);
    }

    [Theory]
    [InlineData(AmigaCombatSpeed.Slow, 840, 1200)]
    [InlineData(AmigaCombatSpeed.Fast, 100, 140)] // 21 >> 2 = 5 ticks, 30 >> 2 = 7
    public void Speed_ScalesEveryWait(AmigaCombatSpeed speed, double attack, double show)
    {
        var line = Run(speed).Lines[0];
        Assert.Equal((attack, show), (line.AttackMs, line.ShowMs));
    }

    [Fact]
    public void Deaths_ShowFromTheirLine_TheDeadMonsterMatchedByWhoIsDeadAfter()
    {
        var run = Run();
        Assert.Empty(run.At(1500).DeadMonsters);           // "Orc dies" prints at 1620
        Assert.Equal([Orc2], run.At(1700).DeadMonsters);   // Orc 1 is alive after the round: Orc 2 died
        Assert.Contains(B, Run(deadBefore: [B]).At(0).DeadMembers);
    }

    // s121 (FORMAT.md §8.6, seen s120): a party spell's flashes play mid-round, each just before its target's lines.
    private static readonly OpenPhantasie.Data.CombatFlashLayout FlashLayout = new(["a", "b", "c"], 6, 1, 60, 8, 3, 2, 36, 22,
        6, 15, 1, 6, -1, 140, 48, [143, 138, 133]); // 3 frames × 7 VBLs at 60 Hz: 350 ms a flash, sound before frame 1

    private static CombatFieldRun SpellRound()
    {
        // A casts Mindblast IV: one Orc takes 56 and dies, the other takes 30; a third hit (0 damage) lands after the
        // round's last line. A resisted target has no hit and no line, so no flash (§8.6).
        string[] log =
        [
            GameText.F("core.p3.x_casts_mindblast", "A"), GameText.F("core.combat.x_takes_x_damage", "Orc", 56),
            GameText.F("core.combat.x_dies", "Orc"), GameText.F("core.combat.x_takes_x_damage", "Orc", 30),
        ];
        CombatLineTag[] tags = [new(0, A, null)];
        CombatHit[] hits =
        [
            new(DamageSource.PartySpell, null, Orc2, Line: 1), new(DamageSource.PartySpell, null, Orc1, Line: 3),
            new(DamageSource.PartySpell, null, Orc1, Line: 4), // a 0-damage hit after the round's last line
        ];
        var flashes = CombatFlashRun.Plan(hits, FlashLayout, _ => 0, _ => 0, _ => false, _ => 0);
        return CombatFieldRun.Plan(log, tags, ["VICTORY"], [A, B], [], [Orc1, Orc2], m => ReferenceEquals(m, Orc2),
            AmigaCombatSpeed.Slow, flashes);
    }

    [Fact]
    public void Flashes_PlayMidRound_EachBeforeItsTargetsTakesLine()
    {
        var run = SpellRound();
        // Cast line 0–1200 (Slow), flash 1200–1550, "takes" 1550–2750, "dies" 2750–3950, flash 3950–4300, "takes"
        // 4300–5500, the last hit's flash 5500–5850, then the line noted after the round.
        Assert.Equal([0.0, 1550, 2750, 4300, 5850], run.Lines.Select(l => l.StartMs));
        Assert.Equal([(Orc2, 1200.0), (Orc1, 3950), (Orc1, 5500)], run.Flashes.Select(f => (f.Flash.Monster, f.StartMs)));
        Assert.Equal(5850 + 1200, run.LengthMs);

        Assert.Null(run.At(1199).Flash);
        Assert.Equal((Orc2, 1), run.At(1200) is { Flash: { } f1 } ? (f1.Flash.Monster, f1.Frames) : default);
        Assert.Equal(3, run.At(1549).Flash!.Value.Frames);
        Assert.False(run.At(1300).TextShown); // the cast line is cleared before the flash
        Assert.Null(run.At(1550).Flash);
        Assert.DoesNotContain(Orc2, run.At(2749).DeadMonsters); // gone from its row as "dies" prints, after its flash
        Assert.Contains(Orc2, run.At(2750).DeadMonsters);
        Assert.Same(Orc1, run.At(4000).Flash!.Value.Flash.Monster);
    }

    [Fact]
    public void Flashes_SoundWithEachFlash_AmongTheSwingSounds()
    {
        // 1200 ms = VBL 72, + frame 1 (7 VBLs); 3950 ms = 237 + 7; 5500 ms = 330 + 7.
        Assert.Equal([(79, 15), (244, 15), (337, 15)], SpellRound().SoundPlays(60));
    }

    [Fact]
    public void Sounds_15ForAHit_18ForAMiss_AsTheLinePrints()
    {
        Assert.Equal([(25, 15), (158, 18)], Run().SoundPlays(60)); // 420 ms, 2640 ms at 60 Hz
    }

    [Fact]
    public void Sounds_21WithACastLine_AsItPrints()
    {
        // s127: III's cast lines start sound 21 as they print (Amiga 0x1620a, 0x17184 …); no attack frame first.
        string[] log = [GameText.F("core.p3.x_casts_fireflash", "Orc"), GameText.F("core.combat.x_takes_x_damage", "A", 4)];
        CombatLineTag[] tags = [new(0, null, Orc1), new(0, null, Orc1, Sound: 21)];
        var run = CombatFieldRun.Plan(log, tags, [], [A, B], [], [Orc1, Orc2], _ => false, AmigaCombatSpeed.Normal);
        Assert.Equal(0, run.Lines[0].AttackMs);
        Assert.Equal([(0, 21)], run.SoundPlays(60));
    }

    [Theory]
    [InlineData(5, 0, false, 24)]   // 3 + 7·0
    [InlineData(5, 4, false, 248)]  // + 4·7 bytes
    [InlineData(3, 0, false, 80)]   // 3 + 7·1
    [InlineData(4, 0, false, 48)]   // 7·((5 − 4) / 2) + 3 + 3 (even)
    [InlineData(2, 1, false, 160)]  // 7·1 + 3 + 3 = 13, + 7
    [InlineData(2, 0, true, 48)]    // giants: 6 for an even count
    [InlineData(1, 0, true, 120)]   // 15 for an odd count
    public void MonsterX_FollowsTheCode(int n, int m, bool giant, int x) =>
        Assert.Equal(x, UiOverlays.FieldMonsterX(m, n, giant));

    [Theory]
    [InlineData(65, true)]  // Wolf: a borrowed normal-size picture (D3-067), still a giant group
    [InlineData(48, true)]  // Mud Elemental
    [InlineData(42, true)]  // giant1's first picture
    [InlineData(41, false)]
    [InlineData(3, false)]  // Giant Bee
    public void Giant_IsRecordByte27Above41_NotThePicture(int byte27, bool giant) =>
        Assert.Equal(giant, UiOverlays.FieldGiant(new Monster { Id = 1, Name = "M", GroupAloneByte = byte27 }));

    [Fact]
    public void AWolf_StandsAtTheGiantPlace_AndItsFlashGetsTheGiantShift()
    {
        var wolf = new Monster { Id = 31, Name = "Wolf", GroupAloneByte = 65 };
        var giant = UiOverlays.FieldGiant(wolf);
        Assert.Equal((120, 50), (UiOverlays.FieldMonsterX(0, 1, giant), UiOverlays.FieldGroupY(0, giant)));
        var layout = new CombatFlashLayout(["a", "b", "c"], 6, 1, 50, 8, 3, 2, 36, 22, 6, 15, 1, 6, -1, 140, 48,
            [143, 138, 133]);
        var run = CombatFlashRun.Plan([new CombatHit(DamageSource.PartySpell, null, wolf)], layout, _ => 0, _ => 0,
            UiOverlays.FieldGiant, _ => 0);
        Assert.Equal((16, 6), (run!.Items[0].X, run.Items[0].Y)); // x jitter 0 + 2 bytes; y 0 + 6
    }

    [Theory]
    [InlineData(65, true, 32, true)]    // Wolf on the Amiga field: the record's flag, as the Amiga
    [InlineData(65, false, 32, false)]  // Wolf on the remaster's rank list: its borrowed 32-px picture, no shift
    [InlineData(65, false, null, false)] // no picture: the placeholder box, no shift
    [InlineData(43, false, 80, true)]   // a real giant picture shifts on both screens
    [InlineData(3, true, 32, false)]
    public void FlashGiant_FollowsThePictureOffTheAmigaField(int byte27, bool amigaField, int? width, bool expected) =>
        Assert.Equal(expected, UiOverlays.FlashGiant(new Monster { Id = 1, Name = "M", GroupAloneByte = byte27 }, amigaField, width));

    [Fact]
    public void Rows_AndPortraits_FollowTheCode()
    {
        Assert.Equal((78, 28, 50), (UiOverlays.FieldGroupY(0, false), UiOverlays.FieldGroupY(1, false), UiOverlays.FieldGroupY(0, true)));
        Assert.Equal((143, 138, 133), (UiOverlays.FieldPortraitY(P3PartyRank.Back), UiOverlays.FieldPortraitY(P3PartyRank.Middle),
            UiOverlays.FieldPortraitY(P3PartyRank.Front)));
        Assert.Equal(96, UiOverlays.FieldPortraitX(2));
    }
}
