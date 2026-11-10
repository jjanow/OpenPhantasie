using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// I Amiga's combat screen (s132, Amiga FORMAT.md "The combat screen"): the pack's layout, the field's places, a round's
/// lines in the Amiga's order with their sounds and waits, and the presenter's menu strip and orders requester.
/// </summary>
public class P1CombatFieldTests
{
    private static CombatFieldLayout Layout() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;

    [OriginalDataFact]
    public void HitLine_TakesTheFieldsTopRows_NotThePartyRow()
    {
        // s183 (seen in the s182 pad pass): the remaster's "X VS Y: HIT n% DMG?n" covered the party row (y 150..181).
        var l = Layout();
        Assert.Equal(15, UiOverlays.P1HitLineTop(l, [32, 40, 38])); // three groups leave the top rows bare
        Assert.Equal(15, UiOverlays.P1HitLineTop(l, []));
        Assert.True(15 + UiOverlays.P1HitLinePitch < l.PartyY);
        Assert.Null(UiOverlays.P1HitLineTop(l, [80, 40, 20])); // a stack up to the frame: left off rather than over a monster
    }

    [OriginalDataFact]
    public void Pack_HasTheCombatScreen_FromTheCode()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;
        var l = pack.CombatField!;
        Assert.Equal(new Box(0, 11, 320, 189), l.Frame);          // h77 +0x6dc(0, 11, 319, 186, 1): 3 px round (0,11)-(319,199)
        Assert.Equal((3, 1), (l.FrameWidth, l.FramePen));
        Assert.Equal(new Box(3, 14, 314, 183), l.Field);          // h58 +0x230 RectFill (3,14)-(316,196)
        Assert.Equal((150, 32, 3), (l.PartyY, l.Slot, l.GroupGap)); // h54 +0x1e6
        Assert.Equal((185, 3), (l.LineY, l.LinePen));             // h80 +0x84a(text, 3, 185)
        Assert.Equal([1, 30, 80], l.SpeedPolls);                  // h65 +0x27e
        Assert.Equal(10, l.StartPolls);                           // h3 +0 at boot
        Assert.Equal((18, 18, 1, 30), (l.AttackTicks, l.FlashTicks, l.FastFlashTicks, l.DeathPolls));
        Assert.Equal((17, 18, 15), (l.FightStartSound, l.RoundEndSound, l.MemberDeathSound));
        Assert.Equal("#EE6600", l.Palette[l.LinePen]);            // E60 ×17: the orange line
        Assert.Equal(new Box(33, 23, 255, 110), l.Orders!.Box);   // requester 21 at Top 23
        Assert.Equal(new Box(42, 114, 235, 9), l.Orders.Fight);   // gadget 1, GRELRIGHT / GRELBOTTOM
        Assert.Equal(6, l.Orders.ChoiceButtons.Count);            // requester 24
        Assert.Equal(new Box(43, 139, 70, 9), l.Orders.ChoiceButtons[0]);
        Assert.Equal(3, l.MenuBar!.Menus.Count);
        Assert.Equal(7, l.MenuBar.Menus[1].Items.Count);
        Assert.EndsWith("combat-grave.png", pack.Resolve(l.Grave!));
        Assert.True(File.Exists(pack.Resolve(l.Grave!)));
        Assert.True(File.Exists(pack.Resolve(PictureNames.MonsterAttacking(1))));
        Assert.True(File.Exists(pack.Resolve(PictureNames.PortraitAttacking(0))));
        var sp = l.Sparkle!;                                      // h53 +0xab8 (s133)
        Assert.Equal((16, 18, 32, (int?)30), (sp.Size, sp.Ticks, sp.DamageStep, sp.Sound));
        Assert.EndsWith("combat-sparkle.png", pack.Resolve(sp.Picture));
        Assert.True(File.Exists(pack.Resolve(sp.Picture)));
        Assert.Equal([0x29A31], pack.DosoundEffects!.Sounds[30]); // h89 +0x1cc plays h90 +0x299 directly
        if (pack.DosoundEffects.TryLoadImage() is { } image)      // 07 fe 08 0f 01 06 82 03 08 0a 01 07 82 01 ff
        {
            var score = pack.DosoundEffects.Score(image, 30)!;
            Assert.Equal(new[] { 3, 1 }, score.Steps.Select(t => t.Ticks));
            Assert.Equal(new[] { 60, 40 }, score.Steps.Select(t => t.Channels[2].Volume)); // A only, B off
            Assert.Equal(AmigaDosoundScore.Period(0x600), score.Steps[0].Channels[2].Period);
            Assert.False(score.Steps[0].Channels[0].Sent);
        }
    }

    [OriginalDataTheory]
    [InlineData(0, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    [InlineData(65, 3)]
    public void Sparkle_Count_IsDamageOver32PlusOne(int damage, int count) =>
        Assert.Equal(count, Layout().Sparkle!.Count(damage));

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(32767, 0, 0)]                                     // a 16-wide monster: no room, rnd(0) = 0
    [InlineData(2046, 16, 0)]                                     // r / (32767 / 16) = r / 2047
    [InlineData(2047, 16, 1)]
    [InlineData(32751, 16, 15)]
    [InlineData(32752, 16, 16)]                                   // the top r reach span itself: the right edge
    [InlineData(32767, 18, 18)]
    public void Sparkle_Offset_IsTheGamesRnd(int r, int span, int offset) =>
        Assert.Equal(offset, CombatFieldSparkle.Offset(r, span));

    [OriginalDataFact]
    public void Sparkle_BeforeTheDamageLine_SoundThenSpriteForEach()
    {
        var l = Layout();
        string[] log =
        [
            GameText.F("core.combat.x_casts_a_spell", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 40),
            GameText.F("core.combat.x_takes_x_damage", "ORC", 5),
        ];
        CombatLineTag[] tags = [new(0, A, null)];
        CombatHit[] hits =
        [
            new(DamageSource.PartySpell, null, Orc1, Line: 1, SpellDamage: 40), // 2 sparkles
            new(DamageSource.PartySpell, null, Orc2, Line: 2, SpellDamage: 5),  // 1
        ];
        var picks = 0;
        var run = P1CombatFieldRun.Plan(l, log, tags, [A], [Orc1, Orc2], _ => false, 2, Hold, hits, n =>
        {
            Assert.Equal(32768, n);
            return picks++;
        });
        Assert.Equal(3, run.Sparkles.Count);
        Assert.Equal(new[] { Orc1, Orc1, Orc2 }, run.Sparkles.Select(s => s.Monster));
        Assert.Equal((0, 1), (run.Sparkles[0].RandX, run.Sparkles[0].RandY));
        var cast = run.Lines[0];
        var first = run.Sparkles[0];
        var f = 1000 / 60.0; // s153: h77 +0xf2a waits 4 WaitTOFs before the sprite shows, +0x108e 3 taking it off
        Assert.Equal(cast.EndMs + 3000 + 4 * f, first.StartMs, 2); // sound 30 first (it blocks: 3 s here)
        Assert.Equal(first.StartMs + 18 * 20 + f, first.EndMs, 2);  // Delay(18) at every Speed, off after a frame
        Assert.Equal(first.EndMs + 2 * f + 3000 + 4 * f, run.Sparkles[1].StartMs, 2);
        Assert.Equal(run.Sparkles[1].EndMs + 2 * f, run.Lines[1].StartMs, 2); // then the damage line
        Assert.Equal(run.Lines[1].EndMs + 3000 + 4 * f, run.Sparkles[2].StartMs, 2);
        Assert.Equal(new[] { 1, 30, 30, 30, 18 }, run.SoundPlays.Select(p => p.Sound));
        Assert.Same(Orc1, run.At(first.StartMs + 1).Sparkle!.Monster);
        Assert.Null(run.At(first.EndMs + 1).Sparkle);            // off before the next sound
        Assert.Null(run.At(run.Lines[1].PrintMs).Sparkle);
    }

    [OriginalDataFact]
    public void NoSparkle_WithoutASpellAmount()
    {
        // Melee, SLEEP, a monster's spell: hits without SpellDamage; the remaster's pack without a sparkle.
        string[] log = [GameText.F("core.combat.x_takes_x_damage", "ORC", 9)];
        CombatHit[] hits = [new(DamageSource.PartyMelee, null, Orc1, Line: 0)];
        Assert.Empty(P1CombatFieldRun.Plan(Layout(), log, [], [A], [Orc1], _ => false, 2, Hold, hits).Sparkles);
        CombatHit[] spell = [new(DamageSource.PartySpell, null, Orc1, Line: 0, SpellDamage: 9)];
        Assert.Empty(P1CombatFieldRun.Plan(Layout() with { Sparkle = null }, log, [], [A], [Orc1], _ => false, 2, Hold, spell)
            .Sparkles);
    }

    [Theory]
    [InlineData(6, new[] { 18, 68, 118, 168, 218, 268 })] // gap (320 − 192)/7 = 18
    [InlineData(3, new[] { 56, 144, 232 })]
    [InlineData(1, new[] { 144 })]
    public void PartyRow_IsSpreadByTheGapRule(int n, int[] xs) =>
        Assert.Equal(xs, Enumerable.Range(0, n).Select(i => CombatFieldLayout.SlotX(i, n, 32)));

    [OriginalDataFact]
    public void Groups_StackUpFromThreeRowsAboveTheParty() =>
        Assert.Equal([117, 92], Layout().GroupTops([30, 22])); // 147 − 30; 117 − 3 − 22

    private static readonly Character A = new() { Name = "A" };
    private static readonly Character B = new() { Name = "B" };
    private static readonly Monster Orc1 = new() { Id = 1, Name = "ORC" };
    private static readonly Monster Orc2 = new() { Id = 1, Name = "ORC" };

    /// <summary>Each sound holds the screen its number × 100 ms, so the tests can tell them apart.</summary>
    private static double Hold(int n) => n * 0.1;

    private static P1CombatFieldRun Round(int speed = 2, Func<int, double>? hold = null)
    {
        // A hits Orc 2, which dies; Orc 1 misses; B takes damage from a cast and dies.
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 9),
            GameText.F("core.combat.x_dies", "ORC"), GameText.F("core.combat.x_misses", "ORC"),
            GameText.F("core.combat.x_casts_a_spell", "ORC"), GameText.F("core.combat.x_takes_x_damage", "B", 7),
            GameText.F("core.combat.x_dies", "B"),
        ];
        CombatLineTag[] tags =
        [
            new(0, A, null, CombatSwing.Hit), new(3, null, Orc1, CombatSwing.Miss), new(4, null, Orc1),
        ];
        return P1CombatFieldRun.Plan(Layout(), log, tags, [A, B], [Orc1, Orc2], m => ReferenceEquals(m, Orc2), speed,
            hold ?? Hold);
    }

    [OriginalDataFact]
    public void Swing_AttackFrame_ThenTheSound_ThenTheLine_ThenTheWait()
    {
        var l = Layout();
        var run = Round();
        var hit = run.Lines[0];
        var s0 = run.SetupMs;                                     // the round's set-up first (s135)
        Assert.Equal((s0, 360.0), (hit.StartMs, hit.AttackMs));   // Delay(18) dos ticks
        Assert.Equal(s0 + 360 + 300, hit.PrintMs, 6);             // sound 3 (party HITS) holds 300 ms here
        Assert.Equal(hit.PrintMs + 30 * l.PollSeconds * 1000, hit.EndMs, 6); // Normal: 30 polls
        Assert.Same(A, run.At(s0 + 100).AttackingMember);
        Assert.Null(run.At(s0 + 400).Text);                       // the sound plays before the text
        Assert.Equal(GameText.F("core.combat.x_hits", "A"), run.At(s0 + 700).Text);
        Assert.Equal(s0, run.SoundPlays[0].AtMs - 360, 6);
        Assert.Equal(3, run.SoundPlays[0].Sound);
    }

    [OriginalDataFact]
    public void LineSounds_FollowTheSide_MonsterDeathErasesFirst()
    {
        var run = Round();
        var sounds = run.SoundPlays.Select(p => p.Sound).ToList();
        // HITS (party) 3, TAKES none, DIES (monster) 16, MISSES (monster) 8, monster cast 9, TAKES none, member death 15, round end 18
        Assert.Equal([3, 16, 8, 9, 15, 18], sounds);
        var dies = run.Lines[2];
        Assert.Same(Orc2, dies.Slain);
        Assert.Contains(Orc2, run.At(dies.StartMs + 1).Gone);     // erased before its sound and line
        Assert.Equal(dies.StartMs + 1600, dies.PrintMs, 6);
    }

    [OriginalDataFact]
    public void Cast_SilhouetteThenAttackFrame()
    {
        var cast = Round().Lines[4];
        Assert.Equal((360.0, 360.0), (cast.FlashMs, cast.AttackMs));
        var run = Round();
        Assert.Same(Orc1, run.At(cast.StartMs + 10).FlashMonster);
        Assert.Same(Orc1, run.At(cast.StartMs + 400).AttackingMonster);
        Assert.Equal(20, Round(speed: 1).Lines[4].FlashMs, 6);   // Fast: Delay(1)
    }

    /// <summary>
    /// D-079 (user s138): a monster's cast line keeps DOS's text but does what the Amiga's one-space line does (h38
    /// <c>+0x480</c>): sound 9 and no polls; the next line replaces it at once. A member's cast still plays 1 and waits.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MonsterCast_KeepsTheText_Sound9_NoWait_D079(int speed)
    {
        var l = Layout();
        Assert.True(l.LineSounds.Single(e => e.Key == "core.combat.x_casts_a_spell").MonsterNoWait);
        var run = Round(speed);
        var cast = run.Lines[4];
        Assert.Equal(GameText.F("core.combat.x_casts_a_spell", "ORC"), cast.Text);
        Assert.Equal(cast.PrintMs, cast.EndMs, 6);                // no polls at any Speed
        Assert.Equal(cast.AttackStartMs + cast.AttackMs + 900, cast.PrintMs, 6); // silhouette, attack frame, then sound 9
        Assert.Equal(cast.EndMs, run.Lines[5].StartMs, 6);        // the spell's own line follows at once
        Assert.Equal(GameText.F("core.combat.x_takes_x_damage", "B", 7), run.At(cast.PrintMs).Text); // no sound: same moment
        Assert.Contains(run.SoundPlays, p => p.Sound == 9 && Math.Abs(p.AtMs - (cast.PrintMs - 900)) < 1e-6);

        string[] log = [GameText.F("core.combat.x_casts_a_spell", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 3)];
        var party = P1CombatFieldRun.Plan(l, log, [new(0, A, null)], [A], [Orc1], _ => false, speed, Hold);
        var member = party.Lines[0];
        Assert.Equal(member.PrintMs + l.LinePolls(speed) * l.PollSeconds * 1000, member.EndMs, 6); // the party's waits
        Assert.Equal(1, party.SoundPlays[0].Sound);
    }

    [OriginalDataFact]
    public void MemberDeath_NoWait_GraveSoundAndPolls_ThenTheRowIsRelaid()
    {
        var l = Layout();
        var run = Round();
        var dies = run.Lines[6];
        Assert.Same(B, dies.Dying);
        Assert.Equal(dies.StartMs, dies.PrintMs, 6);             // sound 15 is not played before the line
        Assert.Equal(dies.PrintMs + 1500 + 30 * l.PollSeconds * 1000, dies.RelaidMs, 6);
        Assert.Contains(B, run.At(dies.PrintMs + 10).Graves);
        Assert.Contains(B, run.At(dies.RelaidMs + 1).Removed);
        Assert.DoesNotContain(B, run.At(dies.RelaidMs + 1).Graves);
        Assert.Equal(dies.RelaidMs + 3 * l.SetupFrameSeconds * 1000 + 1800, run.LengthMs, 6); // the round-end set-up, sound 18
    }

    [OriginalDataFact] // s136: h35 +0x16ac: h38 +0xe72 (re-rank), h71 +0x1050 (the set-up), then h53 +0x2da (sound 18)
    public void RoundEnd_TheSetUpAgain_ThenSound18_TheLineCleared()
    {
        var l = Layout();
        var run = Round();
        var frame = l.SetupFrameSeconds * 1000;
        Assert.Equal(run.Lines[^1].EndMs, run.RoundEndSetupMs!.Value, 6);       // after the last line's wait
        Assert.Equal(CombatFieldLayout.SetupSteps(1, 1), run.RoundEndSetupSteps); // A and Orc 1 still stand
        Assert.Equal((18, run.RoundEndSetupMs.Value + 3 * frame), (run.SoundPlays[^1].Sound, run.SoundPlays[^1].AtMs));
        var mid = run.At(run.RoundEndSetupMs.Value + frame + 1);
        Assert.True(mid.Relaid);
        Assert.Equal(1, mid.SetupDrawn);                                          // the fill and nothing else yet
        Assert.Null(mid.Text);                                                    // the fill takes the line (row 185)
        Assert.Null(run.At(run.RoundEndSetupMs.Value + 3 * frame + 1).SetupDrawn);
        Assert.False(run.At(run.RoundEndSetupMs.Value - 1).Relaid);
        Assert.NotNull(run.At(run.RoundEndSetupMs.Value - 1).Text);
        Assert.Null(run.LastFieldLine);
    }

    [OriginalDataFact] // s136: the last monster's death: h35's exit plays 18 (h53 +0x2da), then state 12 (h44 +0x614) sets up the row only
    public void FightWon_Sound18First_ThenTheRowOnly_ThenTheBoxes()
    {
        var l = Layout();
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_dies", "ORC"),
            GameText.S("core.combat.monsters_killed"),
        ];
        var run = P1CombatFieldRun.Plan(l, log, [new(0, A, null, CombatSwing.Hit)], [A, B], [Orc2], _ => true, 0, Hold);
        var end = run.SoundPlays[^1];
        Assert.Equal(18, end.Sound);
        Assert.Equal(run.Lines[1].EndMs, end.AtMs, 6);
        Assert.Equal(end.AtMs + Hold(18) * 1000, run.RoundEndSetupMs!.Value, 6);
        Assert.Equal(CombatFieldLayout.SetupSteps(2, 0), run.RoundEndSetupSteps);  // the groups emptied (h53 +0xfc)
        Assert.Equal(run.RoundEndSetupMs.Value + 3 * l.SetupFrameSeconds * 1000, run.Lines[2].PrintMs, 6);
        Assert.Equal([GameText.S("core.combat.monsters_killed")], run.Lines[2].Box);
    }

    [OriginalDataFact] // the party's last death: h35's exits to state 6 play 18 only
    public void FightLost_Sound18_NoSetUp()
    {
        string[] log = [GameText.F("core.combat.x_hits", "ORC"), GameText.F("core.combat.x_dies", "A")];
        var run = P1CombatFieldRun.Plan(Layout(), log, [new(0, null, Orc1, CombatSwing.Hit)], [A], [Orc1], _ => false, 0, Hold);
        Assert.Null(run.RoundEndSetupMs);
        Assert.Equal(18, run.SoundPlays[^1].Sound);
    }

    [OriginalDataFact]
    public void SoundOff_NoSoundWaits_TheSpeedPacesTheLines()
    {
        var l = Layout();
        var run = Round(speed: 0, hold: _ => 0);
        var s0 = run.SetupMs;
        Assert.Equal(s0 + 360, run.Lines[0].PrintMs, 6);
        Assert.Equal(s0 + 360 + 10 * l.PollSeconds * 1000, run.Lines[0].EndMs, 6); // boot: 10 polls
        Assert.Equal(l.PollSeconds * 1000, Round(speed: 1, hold: _ => 0).Lines[0].EndMs - s0 - 360, 6); // Fast: 1 poll
        Assert.Equal(run.Lines[^1].EndMs + run.RoundEndSetupSteps * l.SetupFrameSeconds * 1000, run.LengthMs, 6);
    }

    /// <summary>
    /// s135: a round starts with h53 <c>+0x2fc</c>: the fill and each picture after a WaitTOF (NTSC), the row before the
    /// monsters; s132 r1 (six members, one Viper: 8 steps) took 0.14 s live, s134 (five, one: 7) 0.12 s.
    /// </summary>
    [OriginalDataFact]
    public void Round_StartsWithTheSetUp_OnePictureAFrame()
    {
        var l = Layout();
        Assert.Equal(1.0 / 60, l.SetupFrameSeconds, 4);
        Assert.Equal(8, CombatFieldLayout.SetupSteps(6, 1));
        Assert.InRange(CombatFieldLayout.SetupSteps(6, 1) * l.SetupFrameSeconds, 0.12, 0.14);
        Assert.InRange(CombatFieldLayout.SetupSteps(5, 1) * l.SetupFrameSeconds, 0.10, 0.12);
        var run = Round();
        var frame = l.SetupFrameSeconds * 1000;
        Assert.Equal(5 * frame, run.SetupMs, 6);                  // fill + A, B + two orcs
        Assert.Equal(0, run.At(0).SetupDrawn);
        Assert.Equal(1, run.At(frame + 1).SetupDrawn);            // the field cleared
        Assert.Equal(3, run.At(3 * frame + 1).SetupDrawn);        // A and B blitted
        Assert.Null(run.At(run.SetupMs).SetupDrawn);
        // A party option's lines (no round) have no set-up.
        Assert.Equal(0, P1CombatFieldRun.Plan(l, ["MONSTERS RUN"], [], [A], [Orc1], _ => false, 2, _ => 0, roundEnd: false).SetupMs);
    }

    /// <summary>
    /// s135 / s136: a fight that opens with Party Options (state 11, h44 <c>+0xca</c> → h71 <c>+0x1050</c> → h53
    /// <c>+0x274</c>): black while sound 17 holds (1.08 s live with emulator sound off; the model's hold 1.10 s), then one
    /// set-up with the frame last; the bar follows.
    /// </summary>
    [OriginalDataFact]
    public void FightStart_BlackForSound17_ThenOneSetUp()
    {
        var l = Layout();
        var frame = l.SetupFrameSeconds * 1000;
        Assert.Equal(new P1CombatFieldRun.IntroView(true, 0, false), P1CombatFieldRun.Intro(l, 500, 1100, 5, 1));
        Assert.Equal(new P1CombatFieldRun.IntroView(false, 2, false), P1CombatFieldRun.Intro(l, 1100 + 2 * frame + 1, 1100, 5, 1));
        Assert.Null(P1CombatFieldRun.Intro(l, 1100 + 7 * frame + 1, 1100, 5, 1));
        Assert.Equal(new P1CombatFieldRun.IntroView(false, 0, false), P1CombatFieldRun.Intro(l, 0, 0, 5, 1)); // Sound off: no black
    }

    /// <summary>
    /// s136: a surprise opening (round 1 before Party Options, h44 <c>+0x0</c> → state 5) has the fight's first set-up in
    /// round 1 (h35 <c>+0xe</c>): sound 17's black, the set-up, the round, its end set-up and sound 18 (s135's two set-ups
    /// and the bar 0.84 s later).
    /// </summary>
    [OriginalDataFact]
    public void SurpriseOpening_Round1CarriesTheBlack()
    {
        var l = Layout();
        var run = P1CombatFieldRun.Plan(l, [GameText.F("core.combat.x_awakens", "ORC")], [], [A, B], [Orc1], _ => false, 0, Hold,
            leadBlackMs: 1100);
        Assert.True(run.At(500).Black);
        Assert.Equal(0, run.At(1101).SetupDrawn);
        Assert.False(run.At(1101).Black);
        Assert.Equal(1100 + CombatFieldLayout.SetupSteps(2, 1) * l.SetupFrameSeconds * 1000, run.SetupMs, 6);
        Assert.Equal(18, run.SoundPlays[^1].Sound);
        Assert.NotNull(run.RoundEndSetupMs);
    }

    /// <summary>
    /// s138 (live, FS-UAE): after a surprise opening's BltClear the first set-up draws the frame last, and the bar stays
    /// black through round 1, its end set-up and sound 18; the Party Options wait prints it. A normal round keeps both.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(1100)]
    [InlineData(0)] // Sound off: no black, the BltClear all the same
    public void SurpriseOpening_FrameLast_BarClearedForTheWholeRound(double black)
    {
        var l = Layout();
        var run = P1CombatFieldRun.Plan(l, [], [], [A, B], [Orc1], _ => false, 0, Hold, leadBlackMs: black, opensFight: true);
        Assert.True(run.OpensFight);
        Assert.True(run.At(black + 1).FrameHidden);
        var frame = l.SetupFrameSeconds * 1000;
        Assert.Equal(black + (CombatFieldLayout.SetupSteps(2, 1) + 1) * frame, run.SetupMs, 6); // + the frame's PolyDraw
        Assert.True(run.At(run.SetupMs - frame - 1).FrameHidden);
        Assert.Equal(3, run.At(run.SetupMs - frame - 1).SetupDrawn); // the fill, two members
        Assert.False(run.At(run.SetupMs - frame / 2).FrameHidden); // the whole picture with its frame for one frame
        Assert.Equal(4, run.At(run.SetupMs - frame / 2).SetupDrawn); // and the monster
        Assert.False(run.At(run.RoundEndSetupMs!.Value + 1).FrameHidden); // the end set-up's fill leaves the frame
        Assert.All(new[] { 0, black + 1, run.SetupMs, run.LengthMs - 1 }, ms => Assert.True(run.At(ms).BarCleared));
        var normal = P1CombatFieldRun.Plan(l, [], [], [A, B], [Orc1], _ => false, 0, Hold);
        Assert.False(normal.At(1).BarCleared);
        Assert.False(normal.At(1).FrameHidden);
    }

    [OriginalDataFact]
    public void SurpriseOpening_Presenter_ClearsTheBarForRound1Only()
    {
        var s = ClientSessions.Overworld();
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var ui = new CombatPresenter(new ClientSettings())
        {
            P1Layout = Layout(), AmigaP1Field = () => true, P1SoundSeconds = n => n == 17 ? 1.1 : 0,
        };
        ui.Start(s);
        Assert.True(ui.P1OpeningPending);                        // before the first update: the field, black
        Assert.True(ui.P1FieldShown);
        ui.Update(s, 1, fastForward: false);                     // round 1 at once (no Party Options)
        Assert.False(ui.P1OpeningPending);
        var run = ui.P1Run!;
        Assert.True(run.OpensFight);
        Assert.Equal(1100, run.LeadBlackMs, 6);
        Assert.True(ui.P1Shown!.BarCleared);
        ui.Update(s, run.LengthMs + 1, fastForward: false);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);        // the bar comes back with the Party Options wait
        Assert.Null(ui.P1Shown);
    }

    /// <summary>
    /// s165: a won fight's finds are asked on the field (h44 <c>+0x942</c> / <c>+0x956</c>, before h53 <c>+0x2a4</c>): the
    /// front end's after-fight field shows while the presenter is idle, only on I's field, and a new fight drops it.
    /// </summary>
    [OriginalDataFact]
    public void AfterFightField_ShowsWhileIdle_ANewFightDropsIt_s165()
    {
        var s = ClientSessions.Overworld();
        var onField = true;
        var ui = new CombatPresenter(new ClientSettings())
        {
            P1Layout = Layout(), AmigaP1Field = () => onField, P1SoundSeconds = _ => 0,
        };
        Assert.False(ui.P1FieldShown);
        ui.P1AfterFight = new P1CombatFieldSnapshot([.. s.Party.Members], []);
        Assert.True(ui.P1AfterFightShown);
        Assert.True(ui.P1FieldShown);
        onField = false; // AMIGA SCREENS off: the map as before
        Assert.False(ui.P1AfterFightShown);
        onField = true;
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        ui.Start(s);
        Assert.Null(ui.P1AfterFight);
        Assert.False(ui.P1AfterFightShown);
    }

    [OriginalDataFact]
    public void FightStart_PresenterHoldsTheBlack_UntilAKey()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings())
        {
            P1Layout = Layout(), AmigaP1Field = () => true, P1SoundSeconds = n => n == 17 ? 1.1 : 0,
        };
        ui.Start(s);
        Assert.True(ui.P1Intro(6, 1)!.Black);
        ui.Update(s, 1200, fastForward: false);
        Assert.False(ui.P1Intro(6, 1)!.Black);                   // the set-up
        ui.Update(s, 1000, fastForward: false);
        Assert.Null(ui.P1Intro(6, 1));                           // over
        ui.Start(s);
        ui.Update(s, 600, fastForward: false);
        Assert.True(ui.P1Intro(6, 1)!.Black);
        ui.HandleKey(s, Microsoft.Xna.Framework.Input.Keys.Down);
        Assert.Equal(1, ui.P1StartSoundCuts);                    // a key ends sound 17 (s138 live: a click 0.6 s in)
        Assert.Equal(new P1CombatFieldRun.IntroView(false, 0, false), ui.P1Intro(6, 1)); // the set-up still runs
        ui.Update(s, 1000, fastForward: false);
        Assert.Null(ui.P1Intro(6, 1));
        ui.HandleKey(s, Microsoft.Xna.Framework.Input.Keys.Down);
        Assert.Equal(1, ui.P1StartSoundCuts);                    // nothing left to cut
    }

    [OriginalDataFact]
    public void SurpriseOpening_AKeyCutsTheBlack_TheRoundStillRuns()
    {
        var s = ClientSessions.Overworld();
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var ui = new CombatPresenter(new ClientSettings())
        {
            P1Layout = Layout(), AmigaP1Field = () => true, P1SoundSeconds = n => n == 17 ? 1.1 : 0,
        };
        ui.Start(s);
        ui.Update(s, 1, fastForward: false);
        ui.Update(s, 400, fastForward: false);
        Assert.True(ui.P1Shown!.Black);
        ui.P1Click();                                            // a click cuts it as a key does (s138 live)
        Assert.Equal(1, ui.P1StartSoundCuts);
        Assert.Equal(400, ui.P1Run!.LeadBlackMs, 6);
        Assert.False(ui.P1Shown!.Black);
        Assert.True(ui.P1Shown!.BarCleared);
        Assert.Equal(0, ui.P1Shown!.SetupDrawn);                 // the set-up starts there
    }

    [OriginalDataFact]
    public void ShortLines_DoNotWait() // strlen ≤ 3 (the Amiga's " " for a monster's cast)
    {
        var run = P1CombatFieldRun.Plan(Layout(), ["ABC"], [], [A], [], _ => false, 2, _ => 0);
        Assert.Equal(run.Lines[0].PrintMs, run.Lines[0].EndMs);
    }

    [Fact]
    public void OrderRow_IsTheAmigaFormat() =>
        Assert.Equal("EMILY     HP:1   MP:8   PARRY",
            UiOverlays.P1OrderRow(new Character { Name = "EMILY", HpCur = 1, MagicCur = 8 }, "PARRY"));

    private static (GameSession, CombatPresenter) Fight(bool on)
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = Layout(), AmigaP1Field = () => on };
        ui.Start(s);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        return (s, ui);
    }

    [OriginalDataFact]
    public void SwitchOff_TheRemastersScreen_Unchanged()
    {
        var (s, ui) = Fight(on: false);
        Assert.False(ui.P1FieldShown);
        Assert.False(ui.P1FieldMenuShown);
        Assert.False(ui.AmigaFieldMenuShown);
        ui.HandleKey(s, Keys.Enter); // FIGHT, the DOS menu's default
        Assert.Equal(CombatPhase.Orders, ui.Phase);
        Assert.False(ui.P1OrdersShown);
        Assert.Equal(0, ui.OrderMember); // DOS asks the members in turn
    }

    [OriginalDataFact]
    public void SwitchOn_MenuStrip_ThenTheOrdersRequester_ThenARound()
    {
        var (s, ui) = Fight(on: true);
        Assert.True(ui.P1FieldShown);
        Assert.True(ui.P1FieldMenuShown);
        Assert.Null(ui.FieldMenuOpen);
        ui.HandleKey(s, Keys.Down); // Party Options pulled down on Fight (the DOS menu's default)
        Assert.Equal((1, CombatText.DefaultPartyOptionIndex), (ui.FieldMenuOpen, ui.FieldMenuItem));
        ui.HandleKey(s, Keys.Enter);
        Assert.True(ui.P1OrdersShown);
        Assert.Equal(-1, ui.P1PickedMember);

        ui.HandleKey(s, Keys.D1);                 // row 1
        Assert.True(ui.P1AsksOrder);
        ui.HandleKey(s, Keys.T);                  // THRUST (requester 24)
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.StartsWith("THRUST", ui.OrderLabelFor(s, 0));

        ui.HandleKey(s, Keys.F);                  // FIGHT
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        ui.P1Speed = () => 1;
        ui.Update(s, 0, fastForward: false);
        Assert.NotNull(ui.P1Run);
        Assert.True(ui.P1FieldShown);
        ui.Update(s, ui.P1Run!.LengthMs + 1, fastForward: false);
        Assert.Null(ui.P1Run);
        if (s.Mode == GameMode.Combat)
        {
            Assert.True(ui.P1FieldMenuShown);     // the next round's top; the round-end set-up cleared the line (s136)
            Assert.Null(ui.P1LastLine);
        }
    }

    [OriginalDataFact]
    public void OrdersRequester_ListsOkMembersOnly() // s128's 3 / 5 / 6: the others were asleep
    {
        var s = ClientSessions.Fight();
        var members = s.Party.Members;
        members[0].Status = CharacterStatus.Sleep;
        members[1].Status = CharacterStatus.Sleep;
        if (members.Count > 3)
            members[3].Status = CharacterStatus.Sleep;
        var expected = Enumerable.Range(0, members.Count).Where(i => i is not (0 or 1 or 3)).ToList();
        Assert.Equal(expected, CombatPresenter.P1OrderRows(s));
    }

    [OriginalDataTheory] // h80 +0x546: ⌊(h3 + 60·rows) / 60⌋ whole seconds (h44 +0x466 / +0x4d4 / +0x556)
    [InlineData(1, 10, 1)]
    [InlineData(1, 30, 1)]
    [InlineData(1, 80, 2)]
    [InlineData(2, 10, 2)]
    [InlineData(2, 80, 3)]
    [InlineData(3, 1, 3)]
    public void Requester23_StaysWholeSeconds(int rows, int polls, double seconds) =>
        Assert.Equal(seconds, Layout().Message!.Seconds(rows, polls));

    [OriginalDataFact]
    public void Requester23_OneLineTakesTheMiddleRow() // h44 +0x466: rows 101 / 103 blank, the text in 102
    {
        var m = Layout().Message!;
        Assert.Equal([1], m.RowIndices(1));
        Assert.Equal([0, 1], m.RowIndices(2));
        Assert.Equal([80, 88, 96], m.RowsY);
    }

    [OriginalDataFact]
    public void FightEnd_InRequester23_AfterTheRoundEndSound_SharesInOneBox()
    {
        var l = Layout();
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.S("core.combat.monsters_killed"),
            GameText.F("core.combat.x_xp_and_x_gp", 120, 33), GameText.S("core.combat.each_survivor_gets"),
        ];
        var run = P1CombatFieldRun.Plan(l, log, [new(0, A, null, CombatSwing.Hit)], [A, B], [], _ => false, 0, Hold);
        Assert.Equal(3, run.Lines.Count);
        var killed = run.Lines[1];
        var shares = run.Lines[2];
        Assert.Equal([GameText.S("core.combat.monsters_killed")], killed.Box);
        Assert.Equal(l.RoundEndSound, run.SoundPlays[^1].Sound);
        Assert.Equal(run.SoundPlays[^1].AtMs + Hold(l.RoundEndSound!.Value) * 1000, killed.PrintMs, 6); // after sound 18
        Assert.Equal(1000, killed.EndMs - killed.PrintMs, 6);   // boot h3 = 10: ⌊70 / 60⌋ = 1 s
        Assert.Equal([GameText.S("core.combat.each_survivor_gets"), GameText.F("core.combat.x_xp_and_x_gp", 120, 33)], shares.Box);
        Assert.Equal(2000, shares.EndMs - shares.PrintMs, 6);   // ⌊130 / 60⌋ = 2 s
        Assert.Equal(run.LengthMs, shares.EndMs, 6);
        Assert.Equal(shares.Box, run.At(shares.PrintMs + 10).Box);
        Assert.Null(run.At(shares.PrintMs + 10).Text);          // the round-end set-up cleared the line (s135 shot -23)
        Assert.Null(run.At(killed.PrintMs - 1).Box);
        Assert.Null(run.LastFieldLine);
    }

    [OriginalDataTheory] // h80 +0xd2a (s136): ((s & 7)·10⁶ + µs) − the opening's, unsigned: a wrap of the seconds to 8k closes it
    [InlineData(1, 10, 7.5, 0.5)]   // a 1-s box opened at 7.5 s on the clock: closed at 8 s
    [InlineData(1, 10, 2.0, 1.0)]   // no multiple of 8 s inside: the whole second
    [InlineData(1, 10, 15.25, 0.75)]
    [InlineData(2, 10, 6.5, 1.5)]   // a 2-s box crossing 8 s
    [InlineData(2, 10, 6.0, 2.0)]   // ends right at 8 s: the full time
    [InlineData(1, 10, 8.0, 1.0)]   // opened on the multiple: the next is 8 s away
    [InlineData(3, 80, 23.9, 0.1)]  // Slow's 4 s (⌊260 / 60⌋) cut at 24 s
    public void Requester23_ClosesWhenTheClockWrapsEightSeconds(int rows, int polls, double clock, double seconds)
    {
        var m = Layout().Message!;
        Assert.Equal(8, m.ClockWrapSeconds);
        Assert.Equal(seconds, m.ShownSeconds(rows, polls, clock), 6);
        Assert.Equal(m.Seconds(rows, polls), m.ShownSeconds(rows, polls, null)); // clock not modelled
    }

    [OriginalDataFact]
    public void FightEnd_Requester23_ClockWrap_CutsTheBoxThatCrossesIt()
    {
        var l = Layout();
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.S("core.combat.monsters_killed"),
            GameText.S("core.combat.each_survivor_gets"), GameText.F("core.combat.x_xp_and_x_gp", 120, 33),
        ];
        var plain = P1CombatFieldRun.Plan(l, log, [new(0, A, null, CombatSwing.Hit)], [A, B], [], _ => false, 0, Hold);
        var opened = plain.Lines[2].PrintMs;                   // the shares box (2 s without the clock)
        var clock = 8000 - opened - 500 + 80000;               // the clock 0.5 s short of 88 s when it opens
        var run = P1CombatFieldRun.Plan(l, log, [new(0, A, null, CombatSwing.Hit)], [A, B], [], _ => false, 0, Hold,
            clockMs: clock);
        Assert.Equal(1000, run.Lines[1].EndMs - run.Lines[1].PrintMs, 6); // MONSTERS KILLED: no multiple of 8 s inside
        Assert.Equal(500, run.Lines[2].EndMs - run.Lines[2].PrintMs, 6);  // the shares: cut at 88 s
        Assert.Equal(run.Lines[2].EndMs, run.LengthMs, 6);
        Assert.Null(run.At(run.Lines[2].PrintMs + 600).Box);
    }

    [OriginalDataFact]
    public void PartyOption_Requester23_ClockWrap_FromThePresenter()
    {
        var (s, ui) = Fight(on: true);
        ui.P1ClockMs = () => 7800;                 // 0.2 s before 8 s on the machine's clock
        ui.HandleKey(s, Keys.Down);                // Party Options
        while (ui.FieldMenuItem != 2)              // Greetings
            ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter);
        Assert.NotNull(ui.P1Shown?.Box);
        Assert.Equal(200, ui.P1Run!.LengthMs, 6);  // the 1-s answer box cut at 8 s
    }

    [OriginalDataFact]
    public void MonstersRun_FieldLineInARound_BoxOutsideOne() // h35 prints it (sound 2); the morale check's is h44 +0x3a6
    {
        string[] log = [GameText.S("core.combat.monsters_run")];
        var inRound = P1CombatFieldRun.Plan(Layout(), log, [], [A], [], _ => false, 2, Hold);
        Assert.Null(inRound.Lines[0].Box);
        var outside = P1CombatFieldRun.Plan(Layout(), log, [], [A], [], _ => false, 2, Hold, roundEnd: false);
        Assert.NotNull(outside.Lines[0].Box);
        // s153: h44 +0x398 plays 2 before the morale box (it blocks); no line sound, no round end sound.
        Assert.Equal([2], outside.SoundPlays.Select(p => p.Sound));
        Assert.Equal(Hold(2) * 1000, outside.Lines[0].StartMs, 6);
        Assert.Null(outside.LastFieldLine);
    }

    [OriginalDataFact]
    public void PartyOption_AnswerInRequester23_KeysSwallowed_ThenOn()
    {
        var (s, ui) = Fight(on: true);
        ui.HandleKey(s, Keys.Down);                // Party Options
        while (ui.FieldMenuItem != 2)              // Greetings
            ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        Assert.NotNull(ui.P1Run);
        Assert.NotNull(ui.P1Shown?.Box);           // `core.combat.monsters_greet_you` / "DON'T RESPOND" …
        Assert.False(ui.AwaitingInput);
        Assert.False(ui.HandleKey(s, Keys.Enter)); // swallowed
        var length = ui.P1Run!.LengthMs;
        ui.Update(s, length - 1, fastForward: false);
        Assert.NotNull(ui.P1Run);
        var ended = ui.Update(s, 2, fastForward: false);
        Assert.Null(ui.P1Run);
        Assert.True(ended ? ui.Phase == CombatPhase.Idle : ui.Phase is CombatPhase.Orders or CombatPhase.Resolving);
    }

    // --- s133: Cast's spell list (requester 12) and "ON WHOM:" (requester 6) -----------------------------------------------

    [OriginalDataFact]
    public void Pack_SpellList_IsRequester12And6_InTheScreensPens()
    {
        var l = Layout();
        var sp = l.SpellList!.Spells;
        Assert.Equal(new Box(46, 57, 228, 111), sp.Box);              // h84 +0x4506: L46 T57 228×111
        Assert.Equal("#EE0000", l.Palette[sp.FillPen]);               // backfill 6 → 2 (h69 +0x19c), red in shot -07
        Assert.Equal("#00AAEE", l.Palette[sp.BorderPen]);             // border pen 5 → 10
        Assert.Equal("#0044EE", l.Palette[sp.ListFillPen]);           // the list's lines pen 13 → 11
        Assert.Equal(new Box(53, 85, 185, 55), sp.ListFrame);
        Assert.Equal((61, 69), (sp.TitleX, sp.TitleY));               // ReqText at (15, 12)
        Assert.Equal(6, sp.Rows);
        Assert.Equal(new Box(58, 90, 176, 7), sp.Row(0));             // gadget 11 at (12, 33)
        Assert.Equal(new Box(58, 130, 176, 7), sp.Row(5));
        Assert.Equal(new Box(247, 87, 18, 9), sp.ScrollUp);           // GRELRIGHT / GRELBOTTOM −26, −80
        Assert.Equal(new Box(247, 129, 18, 9), sp.ScrollDown);        // −26, −38
        Assert.Equal(new Box(175, 149, 64, 9), sp.Exit);              // −98, −18
        Assert.Equal("#EEBBBB", l.Palette[sp.ScrollBorderPen]);       // pen 1 → 12
        Assert.Equal("#AA4422", l.Palette[sp.ExitBorderPen]);         // pen 2 → 1
        var t = l.SpellList.Target;
        Assert.Equal(new Box(43, 46, 233, 107), t.Box);               // requester 6
        Assert.Equal((0, 1, 15), (t.FillPen, t.BorderPen, t.TitlePen));
        Assert.Equal(7, t.Rows);
        Assert.Equal(new Box(49, 74, 224, 7), t.Row(0));
        Assert.Equal(new Box(204, 135, 64, 9), t.Exit);               // −71, −17
    }

    [Fact]
    public void GhostDots_FollowShot07() // a dot every 4 px, the odd rows shifted by 2
    {
        var dots = UiOverlays.P1GhostDots(new Box(247, 87, 18, 9), 4).ToHashSet();
        Assert.Contains((247, 87), dots);
        Assert.Contains((251, 87), dots);
        Assert.Contains((249, 88), dots);
        Assert.DoesNotContain((248, 87), dots);
        Assert.DoesNotContain((247, 88), dots);
    }

    [OriginalDataFact]
    public void TargetRow_IsTheAmigaFormat() =>
        Assert.Equal("EMILY     SLEEP HP   5/12  MP   3/8  ",
            UiOverlays.P1TargetRow(new Character
            {
                Name = "EMILY", Status = CharacterStatus.Sleep, HpCur = 5, HpMax = 12, MagicCur = 3, MagicMax = 8,
            }));

    /// <summary>The I field at its orders requester with row 1's member knowing <paramref name="spells"/> and <paramref name="mp"/> MP.</summary>
    private static (GameSession, CombatPresenter, int) AtOrders(int[] spells, int mp, CombatFieldLayout? layout = null)
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = layout ?? Layout(), AmigaP1Field = () => true };
        ui.Start(s);
        ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter);              // Party Options › Fight
        Assert.True(ui.P1OrdersShown);
        var m = CombatPresenter.P1OrderRows(s)[0];
        var ch = s.Party.Members[m];
        Array.Clear(ch.SpellsKnown);
        foreach (var id in spells)
            ch.SpellsKnown[id] = 1;
        ch.MagicCur = mp;
        ui.HandleKey(s, Keys.D1);                 // row 1
        Assert.True(ui.P1AsksOrder);
        return (s, ui, m);
    }

    [OriginalDataFact]
    public void Cast_OpensTheSpellList_PagesOfSixInSlotOrder()
    {
        int[] spells = [15, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]; // twelve combat spells
        var (s, ui, _) = AtOrders(spells, mp: 20);
        ui.HandleKey(s, Keys.C);                  // CAST (requester 24)
        Assert.True(ui.P1AsksSpell);
        Assert.Equal([4, 5, 6, 7, 8, 9], ui.P1SpellPage(s)); // ascending slots (h23 +0x570)
        Assert.False(ui.P1SpellCanScrollUp);      // gadget 2 ghosted
        Assert.True(ui.P1SpellCanScrollDown(s));
        ui.HandleKey(s, Keys.Up);                 // ghosted: nothing
        Assert.Equal(0, ui.P1SpellFirst);
        ui.HandleKey(s, Keys.Down);
        Assert.Equal([10, 11, 12, 13, 14, 15], ui.P1SpellPage(s));
        Assert.True(ui.P1SpellCanScrollUp);
        Assert.False(ui.P1SpellCanScrollDown(s));
        ui.HandleKey(s, Keys.Up);
        Assert.Equal(0, ui.P1SpellFirst);
        Assert.Equal(UiText.S("p1field.spell_help"), ui.LastLine);
    }

    [OriginalDataFact]
    public void SpellPick_SetsTheOrder_BackToTheList()
    {
        var (s, ui, m) = AtOrders([4, 8], mp: 5);
        ui.HandleKey(s, Keys.D5);                 // CAST by its digit
        Assert.True(ui.P1AsksSpell);
        ui.HandleKey(s, Keys.D2);                 // QUICKNESS I (slot 8)
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.True(CombatAction.TryDecodeCast(ui.PackedOrder(m), out var spell, out _));
        Assert.Equal(8, spell);
    }

    [OriginalDataFact]
    public void SpellAboveTheMp_IsListed_ItsPickIsParry() // h71 +0x82c: cost slot % 4 + 1 above MP cur → PARRY
    {
        var (s, ui, m) = AtOrders([4, 7], mp: 2);
        ui.HandleKey(s, Keys.C);
        Assert.Equal([4, 7], ui.P1SpellPage(s)); // FIREFLASH IV (4 MP) still listed
        ui.HandleKey(s, Keys.D2);
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.False(CombatAction.TryDecodeCast(ui.PackedOrder(m), out _, out _));
        Assert.Equal("PARRY", ui.OrderLabelFor(s, m));
    }

    [OriginalDataFact]
    public void Exit_IsParry_EscKeepsTheOrder()
    {
        var (s, ui, m) = AtOrders([4], mp: 5);
        ui.HandleKey(s, Keys.T);                  // THRUST first
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.Escape);             // the remaster's way back: no change
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.StartsWith("THRUST", ui.OrderLabelFor(s, m));
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.E);                  // EXIT (gadget 0 → h71 +0x936)
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.Equal("PARRY", ui.OrderLabelFor(s, m));
    }

    [OriginalDataFact]
    public void NoSpells_CastIsParry_NoList()
    {
        var (s, ui, m) = AtOrders([], mp: 5);
        ui.HandleKey(s, Keys.C);
        Assert.False(ui.P1AsksSpell);
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.Equal("PARRY", ui.OrderLabelFor(s, m));
    }

    [OriginalDataFact]
    public void Healing_AsksOnWhom_EveryMemberListed()
    {
        var (s, ui, m) = AtOrders([0], mp: 5);
        var members = s.Party.Members;
        Assert.True(members.Count >= 2);
        members[1].Status = CharacterStatus.Sleep; // not on requester 21, still a target (+0x28e mode 4)
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D1);                 // HEALING I
        Assert.True(ui.P1AsksTarget);
        Assert.Equal(UiText.S("p1field.target_help"), ui.LastLine);
        ui.HandleKey(s, Keys.D2);
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.True(CombatAction.TryDecodeCast(ui.PackedOrder(m), out var spell, out var target));
        Assert.Equal((0, 1), (spell, target));
    }

    [OriginalDataFact]
    public void HealingTarget_Exit_IsParry()
    {
        var (s, ui, m) = AtOrders([0], mp: 5);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.E);
        Assert.Equal("PARRY", ui.OrderLabelFor(s, m));
    }

    [OriginalDataFact]
    public void PackWithoutTheLists_TypesTheNumber_AsBefore()
    {
        var (s, ui, _) = AtOrders([8], mp: 5, Layout() with { SpellList = null });
        ui.HandleKey(s, Keys.C);
        Assert.False(ui.P1AsksSpell);
        ui.HandleKey(s, Keys.D9);
        Assert.Equal("9", ui.SpellDigits);        // requester 26's typed number
    }

    // --- s134: requester 12's tone (h23 +0x734 → h89 +0xd2 = h90 +5) after each gadget event -------------------------------

    [OriginalDataFact]
    public void Pack_Tone_IsH90Plus5_OnRequester12Only()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;
        var lists = pack.CombatField!.SpellList!;
        Assert.Equal(31, lists.Spells.Tone);
        Assert.Null(lists.Target.Tone);                             // "ON WHOM:" (h62 +0x508) plays none
        Assert.Equal([0x2979D], pack.DosoundEffects!.Sounds[31]);  // h90 file 0x29798 + 5
        if (pack.DosoundEffects.TryLoadImage() is { } image)        // 00 2d 01 02 08 0f 07 fe 82 03 ff
        {
            var score = pack.DosoundEffects.Score(image, 31)!;
            Assert.Equal(new[] { 3 }, score.Steps.Select(t => t.Ticks));
            Assert.Equal(60, score.Steps[0].Channels[2].Volume);    // A volume 15 (×4)
            Assert.Equal(AmigaDosoundScore.Period(0x22d), score.Steps[0].Channels[2].Period);
            Assert.False(score.Steps[0].Channels[0].Sent);
        }
    }

    [OriginalDataFact]
    public void Tone_AfterARowAScrollAndExit_NotAGhostedGadgetOrAnEmptyRow()
    {
        int[] spells = [4, 5, 6, 7, 8, 9, 10];    // seven: two pages
        var (s, ui, _) = AtOrders(spells, mp: 20);
        ui.HandleKey(s, Keys.C);
        Assert.Equal(0, ui.P1ToneSerial);         // opening the list plays none
        ui.HandleKey(s, Keys.Up);                 // ghosted: no event
        Assert.Equal(0, ui.P1ToneSerial);
        ui.HandleKey(s, Keys.Down);
        Assert.Equal(1, ui.P1ToneSerial);
        Assert.Equal([10], ui.P1SpellPage(s));
        ui.HandleKey(s, Keys.Down);               // ghosted now
        ui.HandleKey(s, Keys.D3);                 // a row past the list: inert (no GADGIMMEDIATE)
        Assert.Equal(1, ui.P1ToneSerial);
        Assert.True(ui.P1AsksSpell);
        ui.HandleKey(s, Keys.Up);
        Assert.Equal(2, ui.P1ToneSerial);
        ui.HandleKey(s, Keys.Back);               // the remaster's way back: no gadget, no tone
        Assert.Equal(2, ui.P1ToneSerial);
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.E);                  // EXIT
        Assert.Equal(3, ui.P1ToneSerial);
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D2);                 // a pick
        Assert.Equal(4, ui.P1ToneSerial);
        Assert.Equal(-1, ui.P1PickedMember);
    }

    [OriginalDataFact]
    public void Tone_AfterAnOverMpPick_NotOnWhom()
    {
        var (s, ui, m) = AtOrders([0, 7], mp: 2);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D2);                 // FIREFLASH IV above the MP: its row is live (+0xde6), the pick is PARRY
        Assert.Equal(1, ui.P1ToneSerial);
        Assert.Equal("PARRY", ui.OrderLabelFor(s, m));
        ui.HandleKey(s, Keys.D1);
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D1);                 // HEALING I → "ON WHOM:"
        Assert.Equal(2, ui.P1ToneSerial);
        Assert.True(ui.P1AsksTarget);
        ui.HandleKey(s, Keys.D1);                 // requester 6's row: no tone
        Assert.Equal(2, ui.P1ToneSerial);
        Assert.Equal(-1, ui.P1PickedMember);
    }

    [OriginalDataFact]
    public void Tone_HoldsTheRequester_UntilItEnds()
    {
        var (s, ui, m) = AtOrders([4, 8], mp: 5);
        ui.P1SoundSeconds = n => n == 31 ? 0.14 : 0;
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D2);                 // QUICKNESS I
        Assert.True(ui.P1ToneHolding);
        Assert.True(ui.P1AsksSpell);              // still up while the tone plays (the player blocks)
        ui.HandleKey(s, Keys.D1);                 // taken by nobody
        ui.Update(s, 100, fastForward: false);
        Assert.True(ui.P1AsksSpell);
        ui.Update(s, 50, fastForward: false);
        Assert.False(ui.P1ToneHolding);
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.True(CombatAction.TryDecodeCast(ui.PackedOrder(m), out var spell, out _));
        Assert.Equal(8, spell);
        Assert.Equal(1, ui.P1ToneSerial);
    }

    [OriginalDataFact]
    public void NoToneInThePack_ActsAtOnce()
    {
        var l = Layout();
        var lists = l.SpellList!;
        var (s, ui, _) = AtOrders([4, 8], mp: 5, l with { SpellList = lists with { Spells = lists.Spells with { Tone = null } } });
        ui.P1SoundSeconds = _ => 0.14;
        ui.HandleKey(s, Keys.C);
        ui.HandleKey(s, Keys.D1);
        Assert.False(ui.P1ToneHolding);
        Assert.Equal(0, ui.P1ToneSerial);
        Assert.Equal(-1, ui.P1PickedMember);
    }
}
