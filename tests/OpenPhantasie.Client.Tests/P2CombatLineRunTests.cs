using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s136, II ST's combat round line by line (<c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> 7.3): each line's
/// sound, the Speed hold, the sound waits, the member's "DIES" sequence, and the presenter playing a round that way.
/// </summary>
public partial class P2CombatLineRunTests
{
    private const int Fast = 1, Normal = 2, Slow = 3;

    private static StCombatLines Table() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!.StCombatLines!;

    private static readonly Character Hero = new() { Name = "Hero", Status = CharacterStatus.Ok };

    /// <summary>Sound n runs n / 10 seconds (sound 15: 4.2 s), so waits are easy to read.</summary>
    private static double Seconds(int n) => n == 15 ? 4.2 : n / 10.0;

    /// <summary>The table without the 300 ms attack poses (s156), for the tests of the sounds' pacing alone.</summary>
    private static StCombatLines Paced() => Table() with { AttackPoseMs = 0 };

    private static P2CombatLineRun Run(int speed, Func<int, double>? seconds, params string[] lines) =>
        new(Paced(), P2CombatLineRun.Classify(Paced(), lines, [], [Hero]), speed, seconds ?? Seconds);

    [OriginalDataFact]
    public void EndBox_MonstersKilled_HoldsSpeedPlus1000_NoSound()
    {
        // 0xa124: tree 40 shown by 0x16dce(40, 12, 75, S + 1000); no sound call.
        var run = Run(Normal, null, "MONSTERS KILLED");
        Assert.Equal("MONSTERS KILLED", run.ActiveBox!.Text);
        Assert.Empty(run.SoundPlays);
        run.Advance(1009);
        Assert.NotNull(run.ActiveBox);
        run.Advance(1);
        Assert.Null(run.ActiveBox);
        Assert.True(run.Done);
    }

    [OriginalDataFact]
    public void EndBox_OtherLines_HaveNone()
    {
        var run = Run(Normal, null, "Hero HITS");
        Assert.Null(run.ActiveBox);
    }

    [OriginalDataFact]
    public void EndBoxes_PackData_IsOutRscTrees40And41()
    {
        // s138: root ob_spec 0x00020076 (border 2 inward VDI 0 = register 0, fill VDI 6 = register 3), text VDI 0.
        var b = Table().EndBoxes!;
        Assert.Equal((12, 75, 8, 35, 2), (b.X, b.Y, b.Cell, b.CenterColumns, b.Border));
        Assert.Equal(("#000000", "#FF6D00", "#000000"), (b.BorderColour, b.FillColour, b.TextColour));
        Assert.Equal((38, 5, 1000.0), (b.OneLine.Columns, b.OneLine.Rows, b.OneLine.HoldMs));
        Assert.Equal((37, 6, 1500.0), (b.TwoLine.Columns, b.TwoLine.Rows, b.TwoLine.HoldMs));
        Assert.Equal([(1, 1), (1, 3)], b.TwoLine.TextCells);
        Assert.Contains("core.combat.monsters_take_gold", b.Keys);
        Assert.Equal(2, b.SoundFor("core.combat.monsters_run"));
        Assert.Equal(-1, b.SoundFor("core.combat.monsters_killed"));
    }

    [OriginalDataFact]
    public void EndBox_APairsLineAlone_IsTheTwoLineBox_ThePartnersRowEmpty()
    {
        // s158: combat_victory always shows 0xa3da's box; the manual's totals ("%d XP AND %d GP" without "EACH SURVIVOR
        // GETS", p2.combat-shares = manual) take its lower row, the upper row stays empty [~].
        var run = Run(Normal, null, "4220 XP AND 0 GP");
        Assert.Equal(["", "4220 XP AND 0 GP"], run.ActiveBox!.Rows);
        Assert.Equal(1, run.Printed);
        run.Advance(10 + 1499);
        Assert.NotNull(run.ActiveBox); // Speed + 1500 ms, as the pair's box
        run.Advance(1);
        Assert.True(run.Done);
        var minion = Run(Normal, null, "Hero TAKES AN AMULET");
        Assert.Equal(["", "Hero TAKES AN AMULET"], minion.ActiveBox!.Rows);
    }

    [OriginalDataFact]
    public void EndBox_MonstersRun_IsABoxWithSound2OutsideARound_TheSpellsLineInIt()
    {
        // 0xa056: 0xb80e(2), then 0xa25a("MONSTERS RUN"): the morale check's box. In a round: the spell's line (0x84e0).
        var outside = new P2CombatLineRun(Paced(),
            P2CombatLineRun.Classify(Paced(), ["Hero HITS", "MONSTERS RUN"], [], [Hero], outsideRound: true), Normal, Seconds);
        outside.Advance(299); // at normal Speed the box's sound 2 waits for sound 3 (0.3 s here)
        Assert.Null(outside.ActiveBox);
        outside.Advance(1);
        Assert.Equal("MONSTERS RUN", outside.ActiveBox!.Text);
        Assert.Equal([3, 2], outside.SoundPlays);
        Assert.Equal(2, outside.Printed);
        var inside = Run(Normal, null, "MONSTERS RUN");
        Assert.Null(inside.ActiveBox);
        Assert.Equal([2], inside.SoundPlays);
        Assert.Null(P2CombatLineRun.Classify(Table(), ["MONSTERS RUN"], [], [Hero])[0].BoxKey);
    }

    [OriginalDataFact]
    public void Classify_UsesCoreTags_AndDropsBlankLines()
    {
        // A monster named like a member: the tag Core noted for the line decides.
        var orc = new Monster { Name = "Hero" };
        var entries = P2CombatLineRun.Classify(Table(), ["", "Hero HITS"], [new CombatLineTag(1, null, orc, CombatSwing.Hit)],
            [Hero]);
        Assert.Single(entries);
        Assert.Equal(5, entries[0].Sound!.Sound);
    }

    [OriginalDataFact]
    public void Normal_EachSoundWaitsForTheOneBefore_TheLineHolds10Ms()
    {
        // HITS (3, 0.3 s) then ORC HITS (5): the second waits for the first sound, then its own line holds 10 ms.
        var run = Run(Normal, null, "Hero HITS", "ORC HITS");
        Assert.Equal(1, run.Printed);
        Assert.Equal([3], run.SoundPlays);
        run.Advance(10);   // the 10 ms hold is over, but sound 3 runs until 300 ms
        Assert.Equal(1, run.Printed);
        run.Advance(289);
        Assert.Equal(1, run.Printed);
        run.Advance(1);
        Assert.Equal(2, run.Printed);
        Assert.Equal([3, 5], run.SoundPlays);
        run.Advance(9);
        Assert.False(run.Done);
        run.Advance(1);
        Assert.True(run.Done); // the last sound is not waited for (the next sounded line would)
    }

    [OriginalDataFact]
    public void Fast_CutsTheSoundOff_EveryLine5Ms()
    {
        var run = Run(Fast, null, "Hero HITS", "ORC HITS", "Hero MISSES");
        run.Advance(5);
        Assert.Equal(2, run.Printed);
        run.Advance(5);
        Assert.Equal(3, run.Printed);
        Assert.Equal([3, 5, 4], run.SoundPlays);
    }

    [OriginalDataFact]
    public void Slow_HoldsASecond()
    {
        var run = Run(Slow, null, "Hero HITS", "ORC HITS");
        run.Advance(999);
        Assert.Equal(1, run.Printed);
        run.Advance(1);
        Assert.Equal(2, run.Printed); // sound 3 (300 ms) is long over
    }

    [OriginalDataFact]
    public void SoundOff_NoSoundWaits()
    {
        var run = Run(Normal, _ => 0, "Hero HITS", "ORC HITS");
        run.Advance(10);
        Assert.Equal(2, run.Printed);
    }

    [OriginalDataFact]
    public void ALineWithoutSound_DoesNotWaitForTheRunningOne()
    {
        // "TAKES n DAMAGE" (-1) prints after the 10 ms hold although sound 5 still runs; the next sounded line waits.
        var run = Run(Normal, null, "ORC HITS", "Hero TAKES 3 DAMAGE", "Hero MISSES");
        run.Advance(10);
        Assert.Equal(2, run.Printed);
        run.Advance(10);
        Assert.Equal(2, run.Printed); // MISSES waits for sound 5 (500 ms)
        run.Advance(480);
        Assert.Equal(3, run.Printed);
    }

    [OriginalDataFact]
    public void AShortText_HasNoHold()
    {
        // 0x1d1f0: a text of 3 characters or fewer has no hold (an unlisted "ABC" line here).
        var run = Run(Normal, _ => 0, "ABC", "ABCD", "ORC HITS");
        Assert.Equal(2, run.Printed);
        run.Advance(10);
        Assert.Equal(3, run.Printed);
    }

    [OriginalDataFact]
    public void MonsterCast_Sound9_NoHold_MemberCast_PoseFirst()
    {
        var run = Run(Normal, _ => 0, "ORC CASTS A SPELL", "Hero CASTS A SPELL");
        // s156: the monster's special (0x8d8c) shows its silhouette first too (300 ms at Speed > 9); its line stands for the
        // ST's blank line: sound 9, no hold; then the member's silhouette 300 ms (the attack poses are off in Paced()).
        Assert.Equal(0, run.Printed);
        run.Advance(300);
        Assert.Equal(1, run.Printed);
        Assert.Equal([9], run.SoundPlays);
        run.Advance(299);
        Assert.Equal(1, run.Printed);
        run.Advance(1);
        Assert.Equal(2, run.Printed);
        Assert.Equal([9, 1], run.SoundPlays);
        var fast = Run(Fast, _ => 0, "Hero CASTS A SPELL");
        Assert.Equal(0, fast.Printed);
        fast.Advance(10); // the fast pose: 10 ms
        Assert.Equal(1, fast.Printed);
    }

    [OriginalDataFact]
    public void Breath_PlaysTheBlankLinesSound9_Then13()
    {
        var run = Run(Normal, null, "DRAGON BREATHES");
        run.Advance(300); // s156: the special's silhouette first (0x8d8c, mode 2)
        Assert.Equal([9], run.SoundPlays); // 13 waits for 9 (0.9 s)
        Assert.Equal(0, run.Printed);
        run.Advance(900);
        Assert.Equal([9, 13], run.SoundPlays);
        Assert.Equal(1, run.Printed);
    }

    [OriginalDataFact]
    public void MonsterSpecial_AsTracedLive_SilhouettePoseThenSound9_NoHold()
    {
        // s162 (capture_s162_combat.md): 0x186de(group, slot, 2) from 0x8d92: the silhouette 300–315 ms, the attack pose
        // 315 ms, the blank line with sound 9 (no hold: one character), then 0x92f8's own line at once ("TAKES 10 DAMAGE",
        // n −1, the Speed hold). The remaster's "CASTS A SPELL" stands for the blank line (user s137).
        var table = Table();
        var run = new P2CombatLineRun(table, P2CombatLineRun.Classify(table, ["ORC CASTS A SPELL", "Hero TAKES 10 DAMAGE"],
            [], [Hero]), Normal, Seconds);
        Assert.True(run.Pose!.Silhouette);
        run.Advance(300);
        Assert.False(run.Pose!.Silhouette); // the attack pose
        Assert.Equal(0, run.Printed);
        run.Advance(300);
        Assert.Equal([9], run.SoundPlays);
        Assert.Equal(2, run.Printed); // no hold after the special's line, the damage line (no sound) prints at once
        run.Advance(9);
        Assert.False(run.Done);
        run.Advance(1);
        Assert.True(run.Done); // the damage line's 10 ms hold
    }

    [OriginalDataFact]
    public void MonsterBreath_AsTracedLive_13WaitsFor9_ThenTheHold()
    {
        // s162: BABY DRAGON: silhouette, pose, the blank line with 9, then "BABY DRAGON BREATHES" n 13: 13's 0xb8f2 waits
        // for 9 (995 ms live at normal Speed) and the text shows after it, then the Speed hold.
        var table = Table();
        var run = new P2CombatLineRun(table, P2CombatLineRun.Classify(table, ["DRAGON BREATHES", "Hero HITS"], [], [Hero]),
            Normal, Seconds);
        run.Advance(600);
        Assert.Equal([9], run.SoundPlays);
        Assert.Equal(0, run.Printed);
        run.Advance(899);
        Assert.Equal(0, run.Printed);
        run.Advance(1);
        Assert.Equal([9, 13], run.SoundPlays);
        Assert.Equal(1, run.Printed);
        run.Advance(10); // the hold, then Hero's attack pose before HITS
        Assert.Equal(1, run.Printed);
        Assert.NotNull(run.Pose);
    }

    [OriginalDataFact]
    public void MemberDies_MarkSound15_500Ms_WaitForTheSound_ThenTheRowIsRelaid()
    {
        var run = Run(Normal, null, "Hero DIES", "ORC HITS");
        // "DIES": no sound, no hold; the mark; sound 15; 500 ms; wait until sound 15 ends (4.2 s); re-lay.
        Assert.Equal(1, run.Printed);
        Assert.Contains(Hero, run.Marked);
        Assert.Contains(Hero, run.Dying);
        Assert.Equal([15], run.SoundPlays);
        run.Advance(4199);
        Assert.Contains(Hero, run.Marked);
        Assert.DoesNotContain(Hero, run.Removed);
        run.Advance(1);
        Assert.DoesNotContain(Hero, run.Marked);
        Assert.Contains(Hero, run.Removed);
        Assert.Equal(2, run.Printed);
    }

    [OriginalDataFact]
    public void MemberDies_AtFast_OnlyThe500MsHold()
    {
        var run = Run(Fast, null, "Hero DIES");
        run.Advance(499);
        Assert.Contains(Hero, run.Marked);
        run.Advance(1);
        Assert.Contains(Hero, run.Removed);
        Assert.True(run.Done);
    }

    [OriginalDataFact]
    public void AKey_EndsTheHold_NotTheSoundWait()
    {
        var run = Run(Slow, null, "Hero HITS", "ORC HITS");
        run.Key(); // the 1000 ms hold ends at once (Slow does not wait: sound 3 ran 0 ms of 300)
        Assert.Equal(1, run.Printed); // ... but sound 3 still runs: wait for it
        run.Advance(300);
        Assert.Equal(2, run.Printed);
        var death = Run(Normal, null, "Hero DIES");
        death.Key(); // the 500 ms hold after the mark ends, the wait for sound 15 goes on
        death.Advance(4199);
        Assert.Contains(Hero, death.Marked);
        death.Advance(1);
        Assert.True(death.Done);
    }

    [OriginalDataFact]
    public void TakeSounds_ReturnsEachSoundOnce()
    {
        var run = Run(Fast, null, "Hero HITS", "ORC HITS");
        Assert.Equal([3], run.TakeSounds());
        Assert.Empty(run.TakeSounds());
        run.Advance(5);
        Assert.Equal([5], run.TakeSounds());
    }

    [OriginalDataFact]
    public void FightSounds_AreInThePack_17Start_18RoundEnd()
    {
        Assert.Equal(17, Table().FightStartSound);
        Assert.Equal(18, Table().RoundEndSound);
    }

    [OriginalDataFact]
    public void RoundEnds_Sound18PlaysAfterTheLastLine_BeforeTheBoxes()
    {
        // s156: 0x18416 at each exit of combat_round 0x7610 (the round's end, a win, a wipe), before combat_victory's boxes.
        var run = new P2CombatLineRun(Paced(), P2CombatLineRun.Classify(Paced(), ["ORC DIES"], [], [Hero]), Normal, Seconds,
            roundEnds: true);
        run.Finish();
        Assert.Equal([16, 18], run.SoundPlays);
        var won = new P2CombatLineRun(Paced(),
            P2CombatLineRun.Classify(Paced(), ["ORC DIES", "MONSTERS KILLED"], [], [Hero]), Normal, Seconds, roundEnds: true);
        won.Advance(1600); // sound 16 (1.6 s) runs out, then 18 starts and the box shows
        Assert.Equal([16, 18], won.SoundPlays);
        Assert.Equal("MONSTERS KILLED", won.ActiveBox!.Text);
        // Lines outside a round (a party option's answer) play no 18.
        var plain = Run(Normal, null, "ORC DIES");
        plain.Finish();
        Assert.Equal([16], plain.SoundPlays);
    }

    [OriginalDataFact]
    public void Presenter_PlaysSound17AtTheStart_AndSound18LastOnAWin()
    {
        for (var seed = 1; seed < 80; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = Seconds };
            ui.Start(s);
            Assert.Equal([17], ui.TakeP2Sounds());
            Assert.Empty(ui.TakeP2Sounds()); // taken once
            foreach (var m in s.Party.Members)
                m.HpCur = 999;
            foreach (var mon in s.Combat!.AllMonsters)
                mon.HitPoints = 1;
            var played = new List<int>();
            P2CombatLineRun? lastRun = null;
            var rounds = 0;
            for (var round = 0; round < 30 && s.Mode == GameMode.Combat; round++)
            {
                for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                    ui.HandleKey(s, Keys.Enter);
                if (ui.Phase != CombatPhase.Resolving)
                    break;
                s.AutoMeleeRank0();
                ui.Update(s, 0, fastForward: false);
                lastRun = ui.P2Run;
                if (lastRun is not null)
                    rounds++;
                played.AddRange(ui.TakeP2Sounds());
                for (var t = 0; t < 10_000 && ui.IsPresenting && ui.P2Run is not null; t++)
                {
                    ui.Update(s, 10, fastForward: false);
                    played.AddRange(ui.TakeP2Sounds());
                }
                played.AddRange(ui.TakeP2Sounds());
                if (s.Mode != GameMode.Combat)
                    break;
            }
            if (s.Mode == GameMode.Combat || s.Party.Members.All(m => m.Status == CharacterStatus.Dead))
                continue;
            if (lastRun?.Entries[^1].Text != UiText.S("combat.victory"))
                continue; // not a won last round (this seed's fight ended another way)
            Assert.Equal(18, played[^1]); // the boxes after it have no sound
            Assert.Equal(rounds, played.Count(n => n == 18)); // s156: one 18 per round (0x18416)
            return;
        }
        Assert.Fail("no seed won the fight");
    }

    [OriginalDataFact]
    public void Presenter_NoSound17OnTheFirstFightAfterADefeat_AgainAfterAWin()
    {
        // s163, FORMAT.md 7.3: 17 plays only while [0x23eb6] is set (data 1 at load), cleared at the fight's start and set
        // again only at combat_victory's end, which a defeat (mode 6) never reaches.
        var s = ClientSessions.Fight(3);
        var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = _ => 0 };
        ui.Start(s);
        Assert.Equal([17], ui.TakeP2Sounds()); // the first fight of the run
        foreach (var m in s.Party.Members)
        {
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        s.ResolveCombatRound();
        Assert.True(s.LastFightLost);
        void Again()
        {
            foreach (var m in s.Party.Members)
            {
                m.Status = CharacterStatus.Ok;
                m.HpCur = m.HpMax;
            }
            s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
            ui.Start(s);
        }
        Again();
        Assert.Empty(ui.TakeP2Sounds()); // the first fight after the defeat: no 17
        foreach (var mon in s.Combat!.AllMonsters)
            mon.HitPoints = 0;
        s.ResolveCombatRound();
        Assert.False(s.LastFightLost);
        Again();
        Assert.Equal([17], ui.TakeP2Sounds()); // after a won fight: 17 again
    }

    [OriginalDataFact]
    public void Presenter_Sound17IsNotPlayedWithoutThePack()
    {
        var s = ClientSessions.Fight(1);
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.Empty(ui.TakeP2Sounds());
    }

    [Fact]
    public void Options_IITab_HasCombatSpeed_NotOriginalScreens()
    {
        var settings = new ClientSettings();
        var o = new OptionsScreen(settings, _ => [("atari-st", "ATARI ST")], _ => true);
        o.Open();
        o.HandleKeys([Keys.Right, Keys.Right]); // tab II
        Assert.Equal(GameId.P2, o.Tab);
        Assert.Contains(o.RowTexts(), t => t.StartsWith("COMBAT SPEED") && t.EndsWith("NORMAL"));
        Assert.DoesNotContain(o.RowTexts(), t => t.StartsWith("AMIGA SCREENS"));
        for (var k = 0; k < 20 && !o.Lines().Any(l => l.Text.StartsWith(">COMBAT SPEED")); k++)
            o.HandleKeys([Keys.Down]);
        o.HandleKeys([Keys.Enter]);
        Assert.NotEqual(AmigaCombatSpeed.Normal, settings.Game(GameId.P2).CombatSpeed);
        Assert.Equal(AmigaCombatSpeed.Normal, settings.Game(GameId.P1).CombatSpeed); // per game
    }

    [OriginalDataFact]
    public void Presenter_PlaysIIsRoundLineByLine_ThenTheNextMenu()
    {
        for (var seed = 1; seed < 40; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings { TimeLag = 9 }) { P2Lines = Table, P2SoundSeconds = _ => 0 };
            ui.Start(s);
            for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                ui.HandleKey(s, Keys.Enter);
            Assert.Equal(CombatPhase.Resolving, ui.Phase);
            var before = ui.Log.Count;
            ui.Update(s, 0, fastForward: false); // no T)IME LAG pause: the round resolves at once
            if (ui.P2Run is not { } run || run.Entries.Count < 3 || s.Mode != GameMode.Combat)
                continue;
            Assert.Equal(before + run.Printed, ui.Log.Count); // only the printed lines are in the log
            Assert.True(run.Printed < run.Entries.Count);
            while (ui.P2Run is not null)
                ui.Update(s, 10, fastForward: false);
            Assert.Equal(run.Entries.Select(e => e.Text), ui.Log.Skip(before).Take(run.Entries.Count));
            Assert.True(ui.Phase != CombatPhase.Resolving || !ui.IsPresenting);
            return;
        }
        Assert.Fail("no seed gave a round of three lines or more that left the fight going");
    }

    [OriginalDataTheory]
    [InlineData(true)]  // the party wins: the last line is the victory line
    [InlineData(false)] // the party is wiped: every death's sequence plays before the fight ends
    public void Presenter_TheFightsLastRound_PlaysOut_ThenEndsTheFightOnce(bool win)
    {
        for (var seed = 1; seed < 80; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = Seconds };
            ui.Start(s);
            foreach (var m in s.Party.Members)
                m.HpCur = win ? 999 : 1;
            foreach (var mon in s.Combat!.AllMonsters)
                mon.HitPoints = win ? 1 : 999;
            for (var round = 0; round < 30 && s.Mode == GameMode.Combat; round++)
            {
                for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                    ui.HandleKey(s, Keys.Enter);
                if (ui.Phase != CombatPhase.Resolving)
                    break;
                if (win)
                    s.AutoMeleeRank0(); // thrust at rank 1 (Enter alone repeats PARRY)
                var ended = ui.Update(s, 0, fastForward: false);
                Assert.False(ended); // the round's lines play first
                if (ui.P2Run is not { } run)
                    break;
                if (s.Mode == GameMode.Combat)
                {
                    run.Finish();
                    ui.Update(s, 0, fastForward: false);
                    continue;
                }
                // Core has left the fight; the presenter keeps playing the last round.
                Assert.True(ui.IsPresenting);
                var ends = 0;
                for (var t = 0; t < 10_000 && ui.IsPresenting; t++)
                    if (ui.Update(s, 10, fastForward: false))
                        ends++;
                Assert.Equal(1, ends);
                Assert.Null(ui.P2Run);
                Assert.True(run.Done);
                if (win != s.Party.Members.Any(m => m.Status != CharacterStatus.Dead))
                    break; // not the outcome this case is about
                if (win)
                    Assert.Equal(UiText.S("combat.victory"), run.Entries[^1].Text);
                else
                {
                    Assert.NotEmpty(run.Dying);
                    Assert.All(run.Dying, m => Assert.Contains(m, run.Removed));
                    Assert.Empty(run.Marked);
                }
                return;
            }
        }
        Assert.Fail("no seed ended the fight that way");
    }

    [OriginalDataFact]
    public void Presenter_ADyingMember_KeepsItsSlotUntilTheRowIsRelaid()
    {
        for (var seed = 1; seed < 80; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = Seconds };
            ui.Start(s);
            foreach (var m in s.Party.Members)
                m.HpCur = 1;
            for (var round = 0; round < 12 && s.Mode == GameMode.Combat; round++)
            {
                for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                    ui.HandleKey(s, Keys.Enter);
                if (ui.Phase != CombatPhase.Resolving)
                    break;
                ui.Update(s, 0, fastForward: false);
                if (ui.P2Run is not { } run)
                    break;
                if (run.Dying.Count == 0)
                {
                    run.Finish();
                    ui.Update(s, 0, fastForward: false);
                    continue;
                }
                var dying = run.Dying.First();
                Assert.Equal(CharacterStatus.Dead, dying.Status);
                Assert.True(ui.KeepsRowSlot(dying));
                Assert.True(UiOverlays.CombatRowShows(dying, ui.KeepsRowSlot));
                while (ui.P2Run is not null && !run.Removed.Contains(dying))
                    ui.Update(s, 10, fastForward: false);
                Assert.False(ui.KeepsRowSlot(dying));
                Assert.False(ui.HasDeathMark(dying));
                return;
            }
        }
        Assert.Fail("no seed killed a member");
    }

    // ---- s156: the poses (0x186de / 0x188fc), the sparkle (0x18b16), the rock's miss (0x18c6c) ----

    private static readonly Monster Orc = new() { Name = "ORC" };

    [OriginalDataFact]
    public void Pack_HasThePosesTheSparkleAndItsSound()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;
        var t = pack.StCombatLines!;
        Assert.Equal(300, t.AttackPoseMs);
        Assert.Equal([15, 0], t.SilhouettePens);
        Assert.Equal(new StCombatSparkle(35, "combat.sparkle", "combat.rock", 32, 16, 300, 100, 10),
            t.Sparkle! with { RockMissArea = null });
        Assert.Equal([0, 13, 320, 134], t.Sparkle.RockMissArea!); // 0x18458's field from y 13, [0x23eb4] 150 − 3
        Assert.True(pack.StDosoundEffects!.Has(35)); // 0xb9ce's script 0x21c48
        Assert.NotNull(pack.Resolve("combat.sparkle"));
        Assert.NotNull(pack.Resolve("combat.rock"));
        Assert.True(t.Lines["core.combat.x_hits"][CombatLineSide.Monster].Attack);
        Assert.True(t.Lines["core.combat.x_dies"][CombatLineSide.Monster].Erase);
        Assert.True(t.Lines["core.combat.x_breathes"][CombatLineSide.Monster].Pose);
    }

    [Fact]
    public void RockMiss_TheStSpotMovedOntoTheRemastersMonsterArea()
    {
        // s163: (100, 10) of the ST's monster area (0, 13, 320, 134): 100/320 across, 3 px above the top: on a 146 × 80
        // rank box at (8, 92) x = 8 + 100·146/320 = 53, y clamped to the box's top; the sprite kept inside the box.
        var sp = new StCombatSparkle(35, "s", "r", 32, 16, 300, 100, 10) { RockMissArea = [0, 13, 320, 134] };
        Assert.Equal((53, 92), sp.RockMissOn(8, 92, 146, 80, 16, 16));
        Assert.Equal((12, 92), sp.RockMissOn(8, 92, 20, 80, 16, 16)); // 8 + 100·20/320 = 14: the sprite kept inside
        Assert.Equal((8, 92), sp.RockMissOn(8, 92, 10, 80, 16, 16)); // a box narrower than the sprite: its corner
        // Without the area: the ST's own screen spot.
        Assert.Equal((100, 10), (sp with { RockMissArea = null }).RockMissOn(8, 92, 146, 80, 16, 16));
    }

    [OriginalDataFact]
    public void Sparkle_CountAndSpot_AsTheSt()
    {
        var sp = Table().Sparkle!;
        Assert.Equal([1, 1, 2, 4], new[] { 0, 31, 32, 100 }.Select(sp.Count));
        Assert.Equal(1, sp.Count(-3)); // a rock's negative amount
        // 0x18a8a(n) = rand() / (32767 / n): 0..n, 0 when n is 0.
        Assert.Equal((0, 16, 8, 0), (StCombatSparkle.Offset(0, 16), StCombatSparkle.Offset(32767, 16),
            StCombatSparkle.Offset(16384, 16), StCombatSparkle.Offset(12345, 0)));
    }

    [OriginalDataFact]
    public void AttackPose_ASwingHolds300MsBeforeItsSound_AtEverySpeed()
    {
        foreach (var speed in new[] { Fast, Normal, Slow })
        {
            var run = new P2CombatLineRun(Table(), P2CombatLineRun.Classify(Table(), ["Hero HITS"], [], [Hero]), speed, Seconds);
            Assert.Equal(new P2CombatLineRun.PoseShown(Hero, null, false), run.Pose);
            Assert.Empty(run.SoundPlays);
            run.Advance(299);
            Assert.Equal(0, run.Printed);
            run.Advance(1);
            Assert.Null(run.Pose);
            Assert.Equal([3], run.SoundPlays);
            Assert.Equal(1, run.Printed);
        }
    }

    [OriginalDataTheory]
    [InlineData(Normal, 300)]
    [InlineData(Fast, 10)]
    public void CastPose_SilhouetteBySpeed_ThenTheAttackPose300(int speed, double silhouette)
    {
        var run = new P2CombatLineRun(Table(), P2CombatLineRun.Classify(Table(), ["Hero CASTS A SPELL"], [], [Hero]), speed,
            Seconds);
        Assert.True(run.Pose!.Silhouette);
        run.Advance(silhouette);
        Assert.Equal(new P2CombatLineRun.PoseShown(Hero, null, false), run.Pose);
        run.Advance(299);
        Assert.Equal(0, run.Printed);
        run.Advance(1);
        Assert.Equal([1], run.SoundPlays);
        Assert.Equal(1, run.Printed);
    }

    [OriginalDataFact]
    public void MonsterSwing_PosesTheMonsterOfItsTag()
    {
        var entries = P2CombatLineRun.Classify(Table(), ["ORC HITS"], [new CombatLineTag(0, null, Orc, CombatSwing.Hit)], [Hero]);
        Assert.Same(Orc, entries[0].Monster);
        var run = new P2CombatLineRun(Table(), entries, Normal, Seconds);
        Assert.Equal(new P2CombatLineRun.PoseShown(null, Orc, false), run.Pose);
    }

    [OriginalDataFact]
    public void Rock_PoseBeforeTheThrowOnly_TheMissShowsTheRockAtItsSpot_NoSound()
    {
        var run = new P2CombatLineRun(Table(),
            P2CombatLineRun.Classify(Table(), ["Hero THROWS A ROCK", "Hero MISSES"], [], [Hero]), Normal, _ => 0);
        run.Advance(300); // 0x7748: the attack pose, then sound 3 and the line
        Assert.Equal(1, run.Printed);
        run.Advance(10);  // the hold; the MISSES line has no pose of its own (0x78e4)
        Assert.Equal(2, run.Printed);
        Assert.Null(run.Sparkle);
        run.Advance(10);  // MISSES' hold, then 0x18c6c: the rock at (100, 10) for 300 ms
        Assert.Equal(new P2CombatLineRun.SparkleShown(null, true, 0, 0), run.Sparkle);
        Assert.Equal([3, 4], run.SoundPlays);
        run.Advance(300);
        Assert.True(run.Done);
    }

    [OriginalDataFact]
    public void Sparkle_BeforeTheDamageLine_AmountOver32PlusOne_EachSilenced()
    {
        // Fireflash for 40 on the ORC: 40 / 32 + 1 = 2 sparkles, each: sound 35 (through the wait), 300 ms, the silence.
        string[] lines = ["Hero CASTS A SPELL", "ORC TAKES 40 DAMAGE"];
        CombatHit[] hits = [new(DamageSource.PartySpell, null, Orc, Line: 1, SpellDamage: 40)];
        var rolls = new Queue<int>([1000, 2000, 3000, 4000]);
        var run = new P2CombatLineRun(Paced(), P2CombatLineRun.Classify(Paced(), lines, [], [Hero], hits: hits), Normal,
            Seconds, hits: hits, roll: () => rolls.Dequeue());
        run.Advance(300); // the silhouette; sound 1 (0.1 s) and the line
        Assert.Equal(1, run.Printed);
        run.Advance(100); // sound 35 waits for sound 1
        Assert.Equal(new P2CombatLineRun.SparkleShown(Orc, false, 1000, 2000), run.Sparkle);
        run.Advance(300); // the silence: the second sparkle's sound does not wait for 35's long script
        Assert.Equal(new P2CombatLineRun.SparkleShown(Orc, false, 3000, 4000), run.Sparkle);
        Assert.Equal(1, run.Printed);
        run.Advance(300);
        Assert.Null(run.Sparkle);
        Assert.Equal(2, run.Printed);
        Assert.Equal([1, 35, P2CombatLineRun.Silence, 35, P2CombatLineRun.Silence], run.TakeSounds());
        Assert.Equal([1, 35, 35], run.SoundPlays);
    }

    [OriginalDataTheory]
    [InlineData(3, true)]  // a rock's damage: the rock sprite (amount −3)
    [InlineData(0, false)] // a rock of 0 damage: amount 0, the sparkle sprite (0x18b6c bge)
    public void Sparkle_ARocksHit_ShowsTheRock(int damage, bool rock)
    {
        CombatHit[] hits = [new(DamageSource.PartyRock, null, Orc, Line: 1, SpellDamage: damage)];
        var run = new P2CombatLineRun(Paced(),
            P2CombatLineRun.Classify(Paced(), ["Hero THROWS A ROCK", "ORC TAKES 1 DAMAGE"], [], [Hero], hits: hits), Fast,
            _ => 0, hits: hits, roll: () => 0);
        run.Advance(5);
        Assert.Equal(new P2CombatLineRun.SparkleShown(Orc, rock, 0, 0), run.Sparkle);
        // A melee hit or a monster's hit has no sparkle.
        CombatHit[] melee = [new(DamageSource.PartyMelee, null, Orc, Line: 1, SpellDamage: 5)];
        var swing = new P2CombatLineRun(Paced(),
            P2CombatLineRun.Classify(Paced(), ["Hero HITS", "ORC TAKES 1 DAMAGE"], [], [Hero], hits: melee), Fast, _ => 0,
            hits: melee);
        swing.Finish();
        Assert.DoesNotContain(35, swing.SoundPlays);
    }

    [OriginalDataFact]
    public void MonsterDies_ItsPictureIsErasedBeforeTheLine()
    {
        CombatHit[] hits = [new(DamageSource.PartyMelee, Hero, Orc, Line: 1)];
        var entries = P2CombatLineRun.Classify(Paced(), ["ORC TAKES 9 DAMAGE", "ORC DIES"], [], [Hero], hits: hits);
        Assert.Same(Orc, entries[1].Monster);
        var run = new P2CombatLineRun(Paced(), entries, Normal, _ => 0, hits: hits);
        Assert.DoesNotContain(Orc, run.Slain);
        run.Advance(10);
        Assert.Contains(Orc, run.Slain);
        Assert.Equal(2, run.Printed);
    }

    [Fact]
    public void Silhouette_TheShapeInInk_TheRestInPaper()
    {
        Microsoft.Xna.Framework.Color black = new(0, 0, 0), red = new(255, 0, 0), white = new(255, 255, 255);
        Microsoft.Xna.Framework.Color[] px = [black, red, Microsoft.Xna.Framework.Color.Transparent, white];
        AssetCatalog.Silhouette(px, black, white, black);
        Assert.Equal([black, white, black, white], px);
    }

    [OriginalDataFact]
    public void Presenter_RankRows_KeepTheRanksOfBeforeTheRound_LessTheErased()
    {
        for (var seed = 1; seed < 80; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = _ => 0 };
            ui.Start(s);
            Assert.Null(ui.P2RankRows);
            foreach (var m in s.Party.Members)
                m.HpCur = 999;
            foreach (var mon in s.Combat!.AllMonsters)
                mon.HitPoints = 1;
            var before = s.Combat.Ranks.SelectMany(r => r.Members.Where(m => m.IsAlive)).ToList();
            for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                ui.HandleKey(s, Keys.Enter);
            if (ui.Phase != CombatPhase.Resolving)
                continue;
            s.AutoMeleeRank0();
            ui.Update(s, 0, fastForward: false);
            if (ui.P2Run is not { } run)
                continue;
            // At the run's start nothing is erased yet: every monster of before the round shows, dead after it or not.
            Assert.Equal(before.Where(m => !run.Slain.Contains(m)), ui.P2RankRows!.SelectMany(r => r));
            run.Finish();
            if (run.Slain.Count == 0)
                continue;
            Assert.Equal(before.Where(m => !run.Slain.Contains(m)), ui.P2RankRows!.SelectMany(r => r));
            return;
        }
        Assert.Fail("no seed played a round");
    }

    [OriginalDataFact]
    public void MonsterDies_ErasesTheMonsterJustHit_NotAnotherOfItsName()
    {
        // s162: Core notes each monster hit right before its "TAKES" line (CombatResolver.ApplyMonsterDamage), so the
        // "DIES" after it names that very monster, also with several of one kind (a group spell over two ORC ranks).
        var other = new Monster { Name = "ORC" };
        CombatHit[] hits = [new(DamageSource.PartySpell, null, Orc, Line: 1), new(DamageSource.PartySpell, null, other, Line: 3)];
        var entries = P2CombatLineRun.Classify(Paced(),
            ["Hero CASTS A SPELL", "ORC TAKES 9 DAMAGE", "ORC DIES", "ORC TAKES 9 DAMAGE", "ORC DIES"], [], [Hero], hits: hits);
        Assert.Same(Orc, entries[2].Monster);
        Assert.Same(other, entries[4].Monster);
    }

    [Fact]
    public void SlotRects_SideBySide_ThenANewLine_NothingCut()
    {
        // s162: a rank's monsters left to right, 2 px apart, at their own size; one that would run past the width starts a
        // new line, the first of a line always stays (an 80-px picture in a 60-px column is drawn whole).
        var rects = UiOverlays.SlotRects([new(32, 34), new(32, 34), new(32, 34), new(32, 34), new(32, 34)], 2, 10, 140);
        Assert.Equal([2, 36, 70, 104, 2], rects.Select(r => r.X));
        Assert.Equal([10, 10, 10, 10, 46], rects.Select(r => r.Y));
        var big = UiOverlays.SlotRects([new(80, 80), new(80, 80)], 0, 0, 60);
        Assert.Equal([(0, 0), (0, 82)], big.Select(r => (r.X, r.Y)));
        Assert.All(big, r => Assert.Equal((80, 80), (r.Width, r.Height)));
    }

    [OriginalDataFact]
    public void Presenter_RankSlots_OnePerMonster_AnErasedOneLeftEmpty()
    {
        Assert.True(Table().MonsterSlots); // the ST pack: a picture per monster (0x18288)
        for (var seed = 1; seed < 80; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = _ => 0 };
            ui.Start(s);
            // Outside a round: the encounter's living monsters, rank by rank.
            Assert.Equal(s.Combat!.Ranks.Where(r => !r.IsWiped).Select(r => r.Living.Select(m => (Monster?)m).ToList()),
                ui.P2RankSlots(s.Combat)!.Select(r => r.ToList()));
            Assert.Null(new CombatPresenter(new ClientSettings()).P2RankSlots(s.Combat)); // no ST pack: one picture per rank
            foreach (var m in s.Party.Members)
                m.HpCur = 999;
            foreach (var mon in s.Combat.AllMonsters)
                mon.HitPoints = 1;
            var before = s.Combat.Ranks.Select(r => r.Members.Where(m => m.IsAlive).ToList()).Where(r => r.Count > 0).ToList();
            for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                ui.HandleKey(s, Keys.Enter);
            if (ui.Phase != CombatPhase.Resolving)
                continue;
            s.AutoMeleeRank0();
            ui.Update(s, 0, fastForward: false);
            if (ui.P2Run is not { } run)
                continue;
            run.Finish();
            if (!before.Any(r => r.Any(run.Slain.Contains) && !r.All(run.Slain.Contains)))
                continue;
            // 0x18856: the slots keep their places; an erased monster's slot is empty (s163: an emptied rank's row too).
            var expected = before
                .Select(r => r.Select(m => run.Slain.Contains(m) ? null : (Monster?)m).ToList()).ToList();
            Assert.Equal(expected, ui.P2RankSlots(s.Combat)!.Select(r => r.ToList()));
            Assert.Contains(ui.P2RankSlots(s.Combat)!, r => r.Contains(null));
            Assert.Equal(ui.P2RankRows!.Select(r => r.Count), ui.P2RankSlots(s.Combat)!.Select(r => r.Count(m => m is not null)));
            return;
        }
        Assert.Fail("no seed erased one monster of a rank that kept another");
    }
    [OriginalDataFact]
    public void Presenter_ARankWhoseMonstersAreAllErased_KeepsItsEmptyRowUntilTheReRank()
    {
        // s163, the ST: 0x18458 lays the groups out from their counts, which only 0x18288 rewrites at the round's end
        // (0xfd82); an erase (0x186de mode 0) only zeroes the slot's width. So an emptied group's row stays, empty.
        for (var seed = 1; seed < 120; seed++)
        {
            var s = ClientSessions.Fight(seed);
            var ui = new CombatPresenter(new ClientSettings()) { P2Lines = Table, P2SoundSeconds = _ => 0 };
            ui.Start(s);
            foreach (var m in s.Party.Members)
                m.HpCur = 999;
            foreach (var mon in s.Combat!.AllMonsters)
                mon.HitPoints = 1;
            var before = s.Combat.Ranks.Select(r => r.Members.Where(m => m.IsAlive).ToList()).Where(r => r.Count > 0).ToList();
            for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving && ui.IsPresenting; i++)
                ui.HandleKey(s, Keys.Enter);
            if (ui.Phase != CombatPhase.Resolving)
                continue;
            s.AutoMeleeRank0();
            ui.Update(s, 0, fastForward: false);
            if (ui.P2Run is not { } run)
                continue;
            run.Finish();
            var emptied = before.FindIndex(r => r.All(run.Slain.Contains));
            if (emptied < 0)
                continue;
            var slots = ui.P2RankSlots(s.Combat)!;
            Assert.Equal(before.Count, slots.Count); // no row dropped: the rank numbers stay
            Assert.Equal(before[emptied].Count, slots[emptied].Count);
            Assert.All(slots[emptied], Assert.Null);
            Assert.Empty(ui.P2RankRows![emptied]);
            Assert.Same(before[emptied][0], ui.P2RankKinds![emptied]); // its line and slot size
            return;
        }
        Assert.Fail("no seed erased a whole rank");
    }
}
