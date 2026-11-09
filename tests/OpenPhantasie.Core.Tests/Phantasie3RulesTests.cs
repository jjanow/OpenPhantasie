using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie III's Core rules (T4; <c>reverse-engineering/dos/phantasie3/logic-specs/</c>) and the decided D3 entries as
/// <c>p3.*</c> options. Tables come from small fixtures copied from the specs, not from <c>GameData/phantasie3/</c>.
/// </summary>
public class Phantasie3RulesTests
{
    /// <summary>Hands out the given values in order; each must lie in the asked range.</summary>
    private sealed class Script(params int[] values) : IRandomSource
    {
        private int _i;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.True(_i < values.Length, "script ran out");
            var v = values[_i++];
            Assert.InRange(v, minInclusive, maxExclusive - 1);
            return v;
        }

        public bool Done => _i == values.Length;
    }

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P3);
    private static readonly GameOptions Dos = GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);

    private static GameOptions With(string id, string value) => Manual.With(id, value);

    /// <summary><c>body-areas.md</c> §1, <c>DS:0x12cc</c>.</summary>
    private static readonly P3HitLocationTable Locations = new P3HitLocationTable(
    [
        [69, 80, 84, 91, 91, 100],
        [17, 33, 49, 66, 95, 100],
        [3, 11, 35, 49, 80, 100],
        [40, 57, 66, 80, 89, 100],
        [0, 5, 22, 52, 70, 100],
    ]).Validate();

    /// <summary><c>training.md</c> §2–§5 (HP dice for all classes; skill targets for Attack only).</summary>
    private static readonly P3TrainingTables Tables = new(
        ExperienceFactors: [180, 324, 583, 1050, 1890, 3401, 6122, 11020, 19836, 35705, 64268, 115683, 208230, 374813, 674664,
            1214395, 2185911, 3934640, 7082353, 12748236, 22946825],
        TrainingCosts: [1, 5, 11, 21, 34, 52, 72, 97, 126, 158, 195, 237, 282, 332, 387],
        HitDice: new Dictionary<CharacterClass, IReadOnlyList<int>>
        {
            [CharacterClass.Fighter] = [5, 10, 16, 22, 28, 35, 41, 47, 53, 60, 65, 70, 75, 80, 85, 85, 85, 85, 85, 85, 85, 85],
            [CharacterClass.Thief] = [3, 5, 9, 11, 16, 20, 24, 27, 30, 35, 39, 43, 47, 51, 55, 55, 55, 55, 55, 55, 55, 55],
            [CharacterClass.Priest] = [4, 6, 10, 15, 20, 25, 30, 35, 40, 45, 49, 53, 57, 61, 65, 65, 65, 65, 65, 65, 65, 65],
            [CharacterClass.Ranger] = [4, 9, 14, 20, 25, 30, 36, 42, 48, 55, 60, 65, 70, 75, 80, 80, 80, 80, 80, 80, 80, 80],
            [CharacterClass.Monk] = [4, 5, 9, 11, 16, 22, 27, 31, 35, 40, 44, 48, 52, 56, 60, 60, 60, 60, 60, 60, 60, 60],
            [CharacterClass.Wizard] = [3, 4, 6, 8, 11, 14, 17, 19, 22, 25, 28, 31, 34, 37, 40, 40, 40, 40, 40, 40, 40, 40],
        },
        SkillTargets: new Dictionary<P3Skill, IReadOnlyDictionary<CharacterClass, IReadOnlyList<int>>>
        {
            [P3Skill.Attack] = new Dictionary<CharacterClass, IReadOnlyList<int>>
            {
                [CharacterClass.Fighter] = [100, 110, 120, 135, 145, 155, 165, 180, 190, 200, 210, 220, 235, 245, 255],
            },
        });

    // ---- Options ---------------------------------------------------------------------------------------------------

    [Fact]
    public void P3Options_AreIIIs_ManualByDefault_DosInTheDosPreset()
    {
        var ids = RuleOptionRegistry.For(GameId.P3).Select(d => d.Id).ToList();
        Assert.All(P3RuleOptions.All, d => Assert.Contains(d.Id, ids));
        Assert.Equal("guild-limit", ids[^1]);
        Assert.All(P3RuleOptions.All, d => Assert.Equal(GameId.P3, d.Game));
        // The two DOS bugs fixed by default (manual silent, s83) have no difference entry and read "fixed".
        var bugFixes = new[] { P3RuleOptions.Altar, P3RuleOptions.EventGold };
        // III's own D-063 option (user s129) shares I's entry; every other one has a D3 entry.
        Assert.Equal("D-063", P3RuleOptions.CombatSpellList.Difference);
        Assert.All(P3RuleOptions.All.Except(bugFixes).Except([P3RuleOptions.CombatSpellList]), d => Assert.StartsWith("D3-0", d.Difference));
        Assert.All(bugFixes, d => Assert.Null(d.Difference));
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P1), d => d.Id.StartsWith("p3.", StringComparison.Ordinal));
        // D3-003 (user s79: DOS's add) is "add" in every preset; the manual's erase is a player choice.
        Assert.All(new[] { Manual, Dos }, o => Assert.Equal("add", o.Get("p3.transfer-roster")));
        // D3-035's QoL option (user s89) is off in every preset, the Original (DOS) one too.
        Assert.All(new[] { Manual, Dos }, o => Assert.Equal("off", o.Get("p3.transport-planes")));
        foreach (var d in P3RuleOptions.All.Where(d => d != P3RuleOptions.BossFlee && d != P3RuleOptions.TransferRoster
                     && d != P3RuleOptions.TransportPlanes))
        {
            var own = bugFixes.Contains(d) ? "fixed" : "manual";
            Assert.Equal(own, Manual.Get(d.Id));
            Assert.Equal(own, GameOptions.ForPreset(GameId.P3, OptionPreset.Remaster).Get(d.Id));
            Assert.Equal("dos", Dos.Get(d.Id));
        }
        Assert.Equal("blocked", Dos.Get("p3.boss-flee"));
        Assert.Equal(OptionPreset.Custom, Manual.With("p3.boss-flee", "allowed").Preset);
    }

    [Fact]
    public void P3Options_HaveLabels()
    {
        var table = StringTable.ForGame(GameId.P3);
        foreach (var d in P3RuleOptions.All)
        {
            Assert.True(table.Contains("option." + d.Id), d.Id);
            foreach (var v in d.Values)
                Assert.True(table.Contains($"option.{d.Id}.{v}"), $"{d.Id}.{v}");
        }
    }

    [Fact]
    public void D3026_GoldCap_ManualIsTheGames_DosIsUncapped()
    {
        Assert.Equal(GoldCapRule.Manual, Manual.GoldCap);
        Assert.Equal(GoldCapRule.Lifted, Dos.GoldCap);
        Assert.Equal(GoldCapRule.Manual, GameOptions.ForPreset(GameId.P1).GoldCap); // I is untouched
        Assert.Equal(65000, P3Town.ManualGoldCap);
    }

    [Fact]
    public void D3028_Autosave_OnLeavingTown_DosMenuOnly()
    {
        Assert.True(Manual.P3AutoSaveOnLeavingTown());
        Assert.False(Dos.P3AutoSaveOnLeavingTown());
        Assert.False(GameOptions.ForPreset(GameId.P1).P3DosBank()); // a game without III's options plays nothing of III's DOS side
    }

    [Fact]
    public void D3029_Roster_UnlimitedByDefault_Dos17_GuildLimitNoneLiftsIt()
    {
        Assert.Null(Manual.P3RosterCharacterLimit());
        Assert.Equal(17, Dos.P3RosterCharacterLimit());
        Assert.Null(Dos.With("guild-limit", "none").P3RosterCharacterLimit());
        Assert.True(P3Town.RosterHasRoom(500, Manual));
        Assert.True(P3Town.RosterHasRoom(16, Dos));
        Assert.False(P3Town.RosterHasRoom(17, Dos));
    }

    [Fact]
    public void D3020_FoundItems_90_Unlimited_Dos60()
    {
        Assert.Equal(90, Manual.P3FoundItemLimit());
        Assert.Null(With("p3.found-items", "unlimited").P3FoundItemLimit());
        Assert.Equal(60, Dos.P3FoundItemLimit());

        var list = Enumerable.Repeat(7, 89).ToList();
        Assert.True(P3Town.TryAddFoundItem(list, 1, Manual));
        Assert.False(P3Town.TryAddFoundItem(list, 2, Manual));
        Assert.Equal(90, list.Count);
        Assert.True(P3Town.TryAddFoundItem(list, 2, With("p3.found-items", "unlimited")));
        Assert.False(P3Town.TryAddFoundItem(Enumerable.Repeat(7, 60).ToList(), 2, Dos));
    }

    // ---- Body areas --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 10, P3AreaState.Gone)]   // a killing blow always removes an area
    [InlineData(20, 10, P3AreaState.Broken)] // q = 10 / 10 = 1
    [InlineData(30, 10, P3AreaState.Injured)]
    [InlineData(29, 10, P3AreaState.Broken)] // q = 19 / 10 = 1
    public void Severity_ComesFromTheHitAgainstTheHpLeft(int hp, int damage, P3AreaState expected) =>
        Assert.Equal(expected, P3BodyAreas.Severity(hp, damage));

    [Fact]
    public void Severity_SmallHitsNeverInjure()
    {
        Assert.Null(P3BodyAreas.Severity(40, 10));
        var body = new P3BodyAreas();
        Assert.Null(body.TakeHit(40, 10, Locations, P3LocationRow.Melee, new Script())); // no roll spent
        Assert.True(body.AllOkay);
    }

    [Fact]
    public void TakeHit_RollPicksTheArea_OnTheRowsCumulativeOdds_AndAreasOnlyGetWorse()
    {
        var body = new P3BodyAreas();
        // Melee row 2: 3 11 35 49 80 100 → roll 35 = Right Leg, roll 36 = Right Arm, roll 3 = Head.
        var a = body.TakeHit(20, 10, Locations, P3LocationRow.Melee, new Script(35));
        Assert.Equal(new P3Injury(P3BodyArea.RightLeg, P3AreaState.Broken, true), a);
        var b = body.TakeHit(30, 10, Locations, P3LocationRow.Melee, new Script(35)); // Injured on a Broken leg: no change
        Assert.Equal(new P3Injury(P3BodyArea.RightLeg, P3AreaState.Injured, false), b);
        Assert.Equal(P3AreaState.Broken, body[P3BodyArea.RightLeg]);
        Assert.Equal(P3BodyArea.RightArm, Locations.AreaFor(P3LocationRow.Melee, 36));
        Assert.Equal(P3BodyArea.Head, Locations.AreaFor(P3LocationRow.Melee, 3));
        Assert.Equal(P3BodyArea.Torso, Locations.AreaFor(P3LocationRow.Tall, 1)); // row 4 has no Head
        Assert.Equal(P3Status.Down, body.Status(5, P3Status.Okay));
    }

    [Fact]
    public void Status_FromTheBody_InTheSpecsOrder()
    {
        var b = new P3BodyAreas();
        Assert.Equal(P3Status.Okay, b.Status(10, P3Status.Okay));
        Assert.Equal(P3Status.Dead, b.Status(0, P3Status.Okay));
        b[P3BodyArea.LeftLeg] = P3AreaState.Broken;
        Assert.Equal(P3Status.Down, b.Status(10, P3Status.Okay));
        Assert.Equal(P3Status.Asleep, b.Status(10, P3Status.Asleep)); // Asleep is tested before the legs
        b[P3BodyArea.Torso] = P3AreaState.Broken;
        Assert.Equal(P3Status.Out, b.Status(10, P3Status.Asleep));
        b[P3BodyArea.Head] = P3AreaState.Gone;
        Assert.Equal(P3Status.Dead, b.Status(10, P3Status.Okay));
    }

    [Fact]
    public void Restrictions_RightArmForcesParry_AnyLimbBlocksTheBow()
    {
        var b = new P3BodyAreas { [P3BodyArea.LeftArm] = P3AreaState.Broken };
        Assert.False(b.ForcesParry);
        Assert.True(b.BlocksBow);
        b[P3BodyArea.RightArm] = P3AreaState.Gone;
        Assert.True(b.ForcesParry);
        Assert.False(new P3BodyAreas { [P3BodyArea.Head] = P3AreaState.Broken }.BlocksBow);
    }

    [Fact]
    public void D3031_Heal_ManualCuresTheMostVitalFirst_DosPassMovesTheLevel()
    {
        // 4 points, Injured Head, Gone Left Arm, Injured Torso. Manual: the arm (3), then the Head (most vital) with the
        // point left. DOS: the cure inside the pass moves the level to 1 and the same pass reaches the Torso (k 3) first.
        P3BodyAreas Body() => new()
        {
            [P3BodyArea.Head] = P3AreaState.Injured,
            [P3BodyArea.LeftArm] = P3AreaState.Gone,
            [P3BodyArea.Torso] = P3AreaState.Injured,
        };
        var manual = Body();
        Assert.Equal([P3BodyArea.LeftArm, P3BodyArea.Head], manual.Heal(4, Manual));
        Assert.Equal(P3AreaState.Injured, manual[P3BodyArea.Torso]);
        var dos = Body();
        Assert.Equal([P3BodyArea.LeftArm, P3BodyArea.Torso], dos.Heal(4, Dos));
        Assert.Equal(P3AreaState.Injured, dos[P3BodyArea.Head]);
    }

    [Fact]
    public void D3031_Heal_ManualTorsoBeforeArms_AmongEqualStates()
    {
        var b = new P3BodyAreas { [P3BodyArea.RightArm] = P3AreaState.Broken, [P3BodyArea.Torso] = P3AreaState.Broken };
        Assert.Equal([P3BodyArea.Torso], b.Heal(2, Manual));
    }

    [Fact]
    public void D3014_HealingIv_SixPointsCureTwoGoneAreas_DosFourCureOne()
    {
        Assert.Equal(6, P3BodyAreas.HealingCurePoints(4, Manual));
        Assert.Equal(4, P3BodyAreas.HealingCurePoints(4, Dos));
        Assert.Equal(6, P3BodyAreas.PotionCurePoints(10, Manual));
        Assert.Equal(2, P3BodyAreas.PotionCurePoints(5, Manual));

        P3BodyAreas TwoGone() => new() { [P3BodyArea.LeftArm] = P3AreaState.Gone, [P3BodyArea.RightLeg] = P3AreaState.Gone };
        var manual = TwoGone();
        Assert.Equal(2, manual.Heal(P3BodyAreas.HealingCurePoints(4, Manual), Manual).Count);
        Assert.True(manual.AllOkay);
        var dos = TwoGone();
        Assert.Single(dos.Heal(P3BodyAreas.HealingCurePoints(4, Dos), Dos));
        Assert.Equal(P3AreaState.Gone, dos[P3BodyArea.RightLeg]);
    }

    [Fact]
    public void D3015_Inn_OneBreakOrTwoInjuries_DosTownInnHealsAllButGone()
    {
        P3BodyAreas Hurt() => new()
        {
            [P3BodyArea.Head] = P3AreaState.Injured, [P3BodyArea.LeftArm] = P3AreaState.Broken,
            [P3BodyArea.Torso] = P3AreaState.Injured, [P3BodyArea.RightLeg] = P3AreaState.Gone,
        };
        var manual = Hurt();
        Assert.Equal([P3BodyArea.LeftArm], manual.InnStay(inTown: true, Manual));
        var dosTown = Hurt();
        Assert.Equal(3, dosTown.InnStay(inTown: true, Dos).Count);
        Assert.Equal(P3AreaState.Gone, dosTown[P3BodyArea.RightLeg]);
        var dosWild = Hurt();
        Assert.Single(dosWild.InnStay(inTown: false, Dos)); // wilderness / independent inns: 2 points in DOS too
    }

    /// <summary>
    /// s109 DOSBox (`screens-s109/`): one night at The Jolly Inn (independent inn, in-town flag clear) spent 2 cure points
    /// per character — fighto Head I, L Arm B, L Leg I → the arm; fightb Head I, Torso I, R Leg I → Head and Torso;
    /// fightc L Arm Gone, R Leg I → the leg. Both rule sets give the DOS result here.
    /// </summary>
    [Fact]
    public void D3015_WildernessInn_TwoCurePoints_AsSeenInDosBox()
    {
        const P3AreaState I = P3AreaState.Injured, B = P3AreaState.Broken, G = P3AreaState.Gone;
        foreach (var rules in new[] { Manual, Dos })
        {
            var fighto = new P3BodyAreas { [P3BodyArea.Head] = I, [P3BodyArea.LeftArm] = B, [P3BodyArea.LeftLeg] = I };
            Assert.Equal([P3BodyArea.LeftArm], fighto.InnStay(inTown: false, rules));
            var fightb = new P3BodyAreas { [P3BodyArea.Head] = I, [P3BodyArea.Torso] = I, [P3BodyArea.RightLeg] = I };
            Assert.Equal([P3BodyArea.Head, P3BodyArea.Torso], fightb.InnStay(inTown: false, rules).Order());
            Assert.Equal(I, fightb[P3BodyArea.RightLeg]);
            var fightc = new P3BodyAreas { [P3BodyArea.LeftArm] = G, [P3BodyArea.RightLeg] = I };
            Assert.Equal([P3BodyArea.RightLeg], fightc.InnStay(inTown: false, rules));
            Assert.Equal(G, fightc[P3BodyArea.LeftArm]);
        }
    }

    [Fact]
    public void Body_RoundTripsThroughBodyCondition()
    {
        var b = new P3BodyAreas { [P3BodyArea.Torso] = P3AreaState.Broken, [P3BodyArea.RightArm] = P3AreaState.Gone };
        var c = b.ToCondition();
        Assert.Equal("broken", c.Areas["torso"]);
        var back = P3BodyAreas.FromCondition(c);
        Assert.Equal(P3AreaState.Gone, back[P3BodyArea.RightArm]);
        Assert.Equal(P3AreaState.Okay, back[P3BodyArea.Head]);
        Assert.True(P3BodyAreas.Of(new Character()).AllOkay);
        back.ClearAll();
        Assert.True(back.AllOkay);
    }

    // ---- Combat: ranks and the action menu --------------------------------------------------------------------------

    [Fact]
    public void RankTerm_Is30TimesTheDistanceFromTheIntegerAverage()
    {
        P3PartyRank[] party = [P3PartyRank.Front, P3PartyRank.Front, P3PartyRank.Back]; // average 4 / 3 = 1
        Assert.Equal(30, P3Combat.RankTerm(P3PartyRank.Front, party));
        Assert.Equal(-30, P3Combat.RankTerm(P3PartyRank.Back, party));
        Assert.Equal(0, P3Combat.RankTerm(P3PartyRank.Front, [P3PartyRank.Front, P3PartyRank.Front]));
    }

    private static P3Chooser Who(CharacterClass cls, int attack, bool rank2 = true, P3BodyAreas? body = null,
        P3Status status = P3Status.Okay, bool bow = true) =>
        new(cls, attack, body ?? new P3BodyAreas(), status, bow, IsElemental: false, RankTwoExists: rank2);

    [Fact]
    public void Choose_RightArmBroken_AlwaysParries()
    {
        var who = Who(CharacterClass.Fighter, 200, body: new P3BodyAreas { [P3BodyArea.RightArm] = P3AreaState.Broken });
        foreach (var a in Enum.GetValues<P3Action>())
            Assert.Equal(new P3ActionChoice(P3Action.Parry), P3Combat.Choose(a, who, 2, Manual));
    }

    [Fact]
    public void D3010_SlashTooWeak_BecomesAttack_OrThrustUnderDos()
    {
        Assert.Equal(new P3ActionChoice(P3Action.Slash), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Fighter, 150), 1, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Attack), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Fighter, 100), 1, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Fighter, 60), 1, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Fighter, 100), 1, With("p3.slash-fallback", "dos")));
    }

    [Fact]
    public void D3011_Lunge_FightersOnly_DosThievesToo()
    {
        Assert.Equal(new P3ActionChoice(P3Action.Lunge, 2), P3Combat.Choose(P3Action.Lunge, Who(CharacterClass.Fighter, 50), 1, Manual));
        Assert.Equal(P3Action.Thrust, P3Combat.Choose(P3Action.Lunge, Who(CharacterClass.Thief, 50), 1, Manual).Action);
        Assert.Equal(new P3ActionChoice(P3Action.Lunge, 2), P3Combat.Choose(P3Action.Lunge, Who(CharacterClass.Thief, 50), 1, With("p3.lunge", "dos")));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.Lunge, Who(CharacterClass.Fighter, 50, rank2: false), 1, Manual));
        Assert.Equal(P3Action.Thrust, P3Combat.Choose(P3Action.Lunge, Who(CharacterClass.Priest, 50), 2, With("p3.lunge", "dos")).Action);
    }

    [Fact]
    public void D3012_ThiefAttack_PicksTheRank_DosRankOne()
    {
        Assert.Equal(new P3ActionChoice(P3Action.Attack, 2), P3Combat.Choose(P3Action.Attack, Who(CharacterClass.Thief, 100), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Attack, 1), P3Combat.Choose(P3Action.Attack, Who(CharacterClass.Thief, 100), 2, With("p3.thief-attack", "dos")));
        Assert.Equal(new P3ActionChoice(P3Action.Attack, 1), P3Combat.Choose(P3Action.Attack, Who(CharacterClass.Fighter, 100), 2, Manual));
        // Manual-silent chain: a Thief's Slash at rank 2 with Attack % 100 → Attack at rank 2; Attack % 60 → Thrust at rank 2.
        Assert.Equal(new P3ActionChoice(P3Action.Attack, 2), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Thief, 100), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust, 2), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Thief, 60), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust, 1), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Thief, 100), 2, Dos));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust, 2), P3Combat.Choose(P3Action.Thrust, Who(CharacterClass.Thief, 10), 2, Dos));
        Assert.Equal(new P3ActionChoice(P3Action.Slash, 2), P3Combat.Choose(P3Action.Slash, Who(CharacterClass.Thief, 160), 2, Dos));
    }

    [Fact]
    public void D3013_Aim_Needs150AndHitsAtHalf_DosAlwaysAtFull()
    {
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.Aim, Who(CharacterClass.Fighter, 149), 1, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Aim), P3Combat.Choose(P3Action.Aim, Who(CharacterClass.Fighter, 150), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Aim), P3Combat.Choose(P3Action.Aim, Who(CharacterClass.Wizard, 10), 1, Dos));
        Assert.Equal(80, P3Combat.SwingBase(P3Action.Aim, 160, Manual));
        Assert.Equal(160, P3Combat.SwingBase(P3Action.Aim, 160, Dos));
        Assert.Equal(P3LocationRow.Aim, P3Combat.LocationRow(P3Action.Aim, tallMonster: false));
        Assert.Equal(0, P3Combat.DamageModifier(P3Action.Aim));
    }

    [Fact]
    public void FireBow_BlockedByLimbsBowDown_ElseRanked_AndRecheckedAtRoundStart()
    {
        Assert.Equal(new P3ActionChoice(P3Action.FireBow, 2), P3Combat.Choose(P3Action.FireBow, Who(CharacterClass.Priest, 10), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.FireBow, Who(CharacterClass.Priest, 10, bow: false), 2, Manual));
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Choose(P3Action.FireBow, Who(CharacterClass.Priest, 10, status: P3Status.Down), 2, Manual));
        var legBroken = new P3BodyAreas { [P3BodyArea.LeftLeg] = P3AreaState.Broken };
        Assert.Equal(new P3ActionChoice(P3Action.Thrust), P3Combat.Recheck(new(P3Action.FireBow, 2), legBroken, P3Status.Down));
        Assert.Equal(new P3ActionChoice(P3Action.Slash, 2), P3Combat.Recheck(new(P3Action.Slash, 2), legBroken, P3Status.Down));
    }

    [Theory]
    [InlineData(P3Action.Attack, 120, 2, 0)]
    [InlineData(P3Action.Slash, 150, 3, -1)]
    [InlineData(P3Action.Slash, 225, 4, -1)]
    [InlineData(P3Action.Thrust, 120, 1, 1)]
    [InlineData(P3Action.Lunge, 120, 1, 1)]
    public void Swings_AndDamageModifier(P3Action action, int attack, int swings, int modifier)
    {
        Assert.Equal(swings, P3Combat.Swings(action, attack));
        Assert.Equal(modifier, P3Combat.DamageModifier(action));
    }

    // ---- Combat: chances and damage ---------------------------------------------------------------------------------

    [Fact]
    public void D3019_DownAndOutTargets_DoubleCapped95_DosAlwaysHit_AsleepAlways()
    {
        Assert.Equal(100, P3Combat.TargetChance(10, P3Status.Asleep, Manual));
        Assert.Equal(20, P3Combat.TargetChance(10, P3Status.Down, Manual));
        Assert.Equal(95, P3Combat.TargetChance(60, P3Status.Out, Manual));
        Assert.Equal(97, P3Combat.TargetChance(97, P3Status.Down, Manual)); // never below the Okay chance
        Assert.Equal(0, P3Combat.TargetChance(-6, P3Status.Down, Manual)); // a negative chance stays unhittable (open point)
        Assert.Equal(100, P3Combat.TargetChance(-6, P3Status.Down, Dos));
        Assert.Equal(10, P3Combat.TargetChance(10, P3Status.Okay, Dos));
    }

    [Fact]
    public void MeleeChance_FollowsTheSpec()
    {
        // 120 / 2 − (−22) − 40 (Down attacker) + 30 − 15 (rank 2) = 57.
        Assert.Equal(57, P3Combat.MeleeChance(60, -22, attackerDown: true, 30, targetRank: 2, P3Status.Okay, Manual));
        Assert.Equal(1, P3Combat.MeleeChance(10, 50, false, 0, 1, P3Status.Okay, Manual));
        Assert.Equal(2, P3Combat.MeleeChance(10, 50, false, 0, 1, P3Status.Down, Manual));
    }

    [Fact]
    public void ResolveMelee_RunningSum_EverySwingAddsItsDie_HitOrMiss()
    {
        // Attack, Attack % 120, Mace 4: per swing target pick, die, hit roll. Swing 1 die 1, miss (roll 100);
        // swing 2 die 2, hit (roll 1) → 0 + 1 + 2 = 3 (the s81 "after a missed first swing a hit dealt 3").
        var rng = new Script(1, 100, 2, 1);
        var hits = new List<(int, int, P3LocationRow)>();
        var attack = new P3MeleeAttack(P3Action.Attack, 120, WeaponPoints: 4, MonsterDefence: -22, AttackerDown: false,
            RankTerm: 0, TargetRank: 1, TallMonster: false);
        var swings = P3Combat.ResolveMelee(attack, _ => 0, _ => P3Status.Okay, (t, d, r) => hits.Add((t, d, r)), rng, Manual);
        Assert.True(rng.Done);
        Assert.Equal(2, swings.Count);
        Assert.False(swings[0].Hit);
        Assert.Equal(82, swings[1].Chance);
        Assert.Equal([(0, 3, P3LocationRow.Melee)], hits);
    }

    [Theory]
    [InlineData(P3Status.Down)]
    [InlineData(P3Status.Out)]
    public void D3019_DosLive_s110_ThrustOnDownOrOutMonster_AlwaysHits_DamageUnchanged(P3Status target)
    {
        // The s110 DOSBox setup: Thrust at Attack % 61 with a Mace (4) against defence 30 = 31 % on an Okay Skeleton
        // (control: 4 hits in 18); on Down (right leg Broken) / unconscious Skeletons every thrust hit (18 of 18) for
        // 2..5 = 1 + rnd(4), no bonus. Per swing: target pick, die, hit roll.
        var attack = new P3MeleeAttack(P3Action.Thrust, 61, WeaponPoints: 4, MonsterDefence: 30, AttackerDown: false,
            RankTerm: 0, TargetRank: 1, TallMonster: false);
        var hits = new List<(int, int, P3LocationRow)>();
        var rng = new Script(4, 100);
        var swings = P3Combat.ResolveMelee(attack, _ => 0, _ => target, (t, d, r) => hits.Add((t, d, r)), rng, Dos);
        Assert.True(rng.Done);
        Assert.Equal(100, swings[0].Chance);
        Assert.Equal([(0, 5, P3LocationRow.Melee)], hits);

        var okay = P3Combat.ResolveMelee(attack, _ => 0, _ => P3Status.Okay, (_, _, _) => { }, new Script(4, 100), Dos);
        Assert.Equal(31, okay[0].Chance);
        Assert.False(okay[0].Hit);
    }

    [Fact]
    public void WeaponDie_BareHandsIsAttackOver25()
    {
        Assert.Equal(4, P3Combat.WeaponDie(4, 120));
        Assert.Equal(4, P3Combat.WeaponDie(0, 100));
        Assert.Equal(1, P3Combat.WeaponDie(0, 10));
    }

    [Fact]
    public void D3018_Defence_ArmourProtects_DosSignMatchesTheLiveValues()
    {
        // s81 live: Small Shield (4), Parry 40, DEX 17; no armour 46, item 35 (6 points) 16 under DOS.
        Assert.Equal(46, P3Combat.Defence(4, 0, 40, 17, Dos));
        Assert.Equal(16, P3Combat.Defence(4, 6, 40, 17, Dos));
        Assert.Equal(76, P3Combat.Defence(4, 6, 40, 17, Manual));
        // Wild Cat, attack 40: one attack; −6 % against defence 46, 24 % against 16 (DOS).
        Assert.Equal(1, P3Combat.MonsterAttacks(40));
        Assert.Equal(-6, P3Combat.MonsterChance(40, 46, false, 0, false, 4, P3Status.Okay, Dos));
        Assert.Equal(24, P3Combat.MonsterChance(40, 16, false, 0, false, 4, P3Status.Okay, Dos));
        Assert.Equal(100, P3Combat.MonsterChance(40, 54, false, 0, false, 20, P3Status.Down, Dos)); // s81: 14 of 14 hit
        Assert.Equal(20, P3Combat.MonsterChance(40, 16, false, 0, parrying: true, 4, P3Status.Okay, Dos)); // − shield points
        Assert.Equal(2, P3Combat.MonsterAttacks(150));
    }

    [Fact]
    public void D3017_ArrowFlame_AddsToBowPoints_DosReplacesTheRange()
    {
        Assert.Equal(new P3BowStats(60, 44), P3Combat.BowStats(20, 60, P3BowSpell.None, Manual));
        Assert.Equal(new P3BowStats(60, 4), P3Combat.BowStats(0, 60, P3BowSpell.None, Manual));
        Assert.Equal(new P3BowStats(110, 74), P3Combat.BowStats(20, 60, P3BowSpell.ArrowFlameI, Manual));
        Assert.Equal(new P3BowStats(110, 35), P3Combat.BowStats(20, 60, P3BowSpell.ArrowFlameI, Dos)); // lower than 44
        Assert.Equal(new P3BowStats(160, 94), P3Combat.BowStats(20, 60, P3BowSpell.ArrowFlameII, Manual));
        Assert.Equal(new P3BowStats(160, 45), P3Combat.BowStats(20, 60, P3BowSpell.ArrowFlameII, Dos));
        Assert.Equal(new P3BowStats(250, 44), P3Combat.BowStats(20, 60, P3BowSpell.Accuracy, Dos));
    }

    [Fact]
    public void Bow_HalfDefence_NoRankTerm_AndTheShot()
    {
        Assert.Equal(71, P3Combat.BowChance(60, -22, P3Status.Okay, Manual));
        Assert.Equal(1, P3Combat.BowChance(10, 100, P3Status.Okay, Manual));
        Assert.Equal(7, P3Combat.FireBow(new P3BowStats(60, 44), -22, P3Status.Okay, new Script(71, 7), Manual));
        Assert.Equal(0, P3Combat.FireBow(new P3BowStats(60, 44), -22, P3Status.Okay, new Script(72), Manual));
        Assert.True(P3Combat.CanUseBow(new CharacterStats(10, 0, 12, 0, 0, 0), 22));
        Assert.False(P3Combat.CanUseBow(new CharacterStats(10, 0, 11, 0, 0, 0), 22));
        // Fire Bow seed: 2·1 + 4·15 + 12 + 10 + 30 (Thief) + 15 (Elf) = 129.
        Assert.Equal(129, P3Combat.FireBowSeed(1, new CharacterStats(12, 0, 15, 0, 0, 0), CharacterClass.Thief, Race.Elf));
    }

    // ---- Encounters --------------------------------------------------------------------------------------------------

    [Fact]
    public void D3021_UnconsciousMonsterShowsU_DosO()
    {
        Assert.Equal('U', P3Encounters.StatusLetter(P3Status.Out, Manual));
        Assert.Equal('O', P3Encounters.StatusLetter(P3Status.Out, Dos));
        Assert.Equal('S', P3Encounters.StatusLetter(P3Status.Asleep, Manual));
        Assert.Equal('D', P3Encounters.StatusLetter(P3Status.Down, Manual));
        Assert.Equal('K', P3Encounters.StatusLetter(P3Status.Okay, Dos));
    }

    [Fact]
    public void Strength_CountsMembersWithOddHp_AsDos()
    {
        Assert.Equal(3 * 2 + 5, P3Encounters.Strength([new(3, [7, 8, 9, 0]), new(5, [1])]));
    }

    [Fact]
    public void D3024_GreetMercySurrender_AnyMonster_DosBlocksFrom44()
    {
        Assert.True(P3Encounters.Greet(59, 8, new Script(47), Manual)); // High Ranger, Good: 47 < 48
        Assert.False(P3Encounters.Greet(59, 8, new Script(), Dos));      // DOS: blocked, no roll
        Assert.False(P3Encounters.Greet(10, 8, new Script(48), Manual));
        Assert.False(P3Encounters.Greet(10, 8, new Script(), Manual, greetBlockedFlag: true));
        Assert.True(P3Encounters.BegForMercy(50, 0, new Script(59), Manual));
        Assert.False(P3Encounters.BegForMercy(50, 0, new Script(59), Dos));
        Assert.True(P3Encounters.Surrenders(4, 10, 60, new Script(50), Manual));
        Assert.False(P3Encounters.Surrenders(4, 10, 60, new Script(50), Dos));
        Assert.False(P3Encounters.Surrenders(5, 10, 10, new Script(), Manual)); // not below half: no roll
    }

    [Fact]
    public void D3024_Mercy_TakesEachNewItemAt50_GoldOnlyOption()
    {
        var found = new LootSack();
        found.ItemIds.AddRange([120, 0, 121]);
        found.MarkTownExit(); // 120 and 121 were found on an earlier trip
        found.ItemIds[1] = 101; // finds fill the hole first, then append
        found.ItemIds.AddRange([102, 103, 104]);
        // Only the four new slots roll (≤ 50 taken); an empty slot never rolls.
        Assert.Equal(2, P3Encounters.MercyTakeItems(found, new Script(50, 51, 1, 100), Manual));
        Assert.Equal([120, 0, 121, 102, 0, 104], found.ItemIds); // taken items leave holes, as I's Mercy
        Assert.Equal(0, P3Encounters.MercyTakeItems(found, new Script(), With("p3.mercy", "dos")));
        Assert.Equal(0, P3Encounters.MercyTakeItems(found, new Script(), Dos)); // DOS preset: gold only
        found.ItemIds[^1] = 0;
        found.ItemIds.Add(0);
        Assert.Equal(1, P3Encounters.MercyTakeItems(found, new Script(1), Manual)); // 102; trailing holes trimmed
        Assert.Equal([120, 0, 121], found.ItemIds);
    }

    [Fact]
    public void D3024_Flee_NoIdBlock_FinalBossesBlock_UnlessAllowed()
    {
        var party = new P3FleeContext(RankOneExists: true, RankOneAllDisabled: false, AnyMemberAble: true);
        Assert.True(P3Encounters.Flee(party, 60, 0, 10, new Script(59), Manual)); // 59 < 70 + 0 − 10
        Assert.False(P3Encounters.Flee(party, 60, 0, 10, new Script(), Dos));
        Assert.True(P3Encounters.Flee(party, 10, 0, 10, new Script(59), Dos));
        Assert.False(P3Encounters.Flee(party, 77, 0, 10, new Script(1), Manual));
        Assert.True(P3Encounters.Flee(party, 79, 0, 10, new Script(1), With("p3.boss-flee", "allowed")));
        var disabled = party with { RankOneAllDisabled = true };
        Assert.True(P3Encounters.Flee(disabled, 60, 0, 10, new Script(), Dos));
        Assert.False(P3Encounters.Flee(disabled, 78, 0, 10, new Script(), Dos.With("p3.boss-flee", "allowed")));
        Assert.False(P3Encounters.Flee(party with { AnyMemberAble = false }, 10, 0, 0, new Script(), Manual));
    }

    // ---- Training ----------------------------------------------------------------------------------------------------

    private static P3Trainee Fighter(int level = 1, int con = 10, long xp = 0, int score = 0, int social = 2, int hpMax = 10) =>
        new(CharacterClass.Fighter, new CharacterStats(15, 10, 15, con, 10, 10), level, xp, score, social, hpMax,
            P3Status.Okay, IsUndead: false);

    [Fact]
    public void MayTrain_RefusesDeadOutUndeadAndLevel21()
    {
        Assert.True(P3Training.MayTrain(Fighter()));
        Assert.False(P3Training.MayTrain(Fighter() with { Status = P3Status.Out }));
        Assert.False(P3Training.MayTrain(Fighter() with { IsUndead = true }));
        Assert.False(P3Training.MayTrain(Fighter(level: 21)));
        Assert.True(P3Training.MayTrain(Fighter() with { Status = P3Status.Down }));
    }

    [Fact]
    public void ExperienceNeeded_AndCost()
    {
        Assert.Equal(136, P3Training.ClassScore(CharacterClass.Fighter, Fighter().Stats)); // 45·100/33
        Assert.Equal(17, P3Training.ClassScore(CharacterClass.Thief, new CharacterStats(1, 1, 1, 1, 1, 1)));
        Assert.Equal(735, P3Training.ExperienceNeeded(Fighter(), Tables)); // (180000 − 80000) / 136
        Assert.Equal(367, P3Training.ExperienceNeeded(Fighter(score: 200), Tables));
        Assert.Equal(122, P3Training.ExperienceNeeded(Fighter(score: 250), Tables));
        Assert.Equal(35, P3Training.Shortfall(Fighter(xp: 700), Tables));
        Assert.Equal(0, P3Training.Shortfall(Fighter(xp: 800), Tables));
        Assert.Equal(150, P3Training.Cost(1, 10, 150, Tables));
        Assert.Equal(5, P3Training.Cost(1, 20, 150, Tables));       // (20 − CHA)² = 0 → the 5-gold minimum
        Assert.Equal(15000, P3Training.Cost(15, 0, 150, Tables));
        Assert.Equal(1 * 150 * 4 / 100, P3Training.Cost(1, 22, 150, Tables)); // not clamped: CHA 22 gives 4
    }

    [Fact]
    public void D3022_HitDice_ScaleWithCon_Con10IsTheChart_DosIgnoresCon()
    {
        foreach (var (_, dice) in Tables.HitDice)
            foreach (var d in dice)
            {
                Assert.Equal(d, P3Training.HitDiceCount(d, 10, Manual));
                Assert.Equal(d, P3Training.HitDiceCount(d, 18, Dos));
            }
        Assert.Equal(7, P3Training.HitDiceCount(5, 16, Manual)); // 5·21/15
        Assert.Equal(2, P3Training.HitDiceCount(5, 3, Manual));  // 5·8/15
        // Creation: level-1 dice, one rnd(2) + 1 each. Fighter CON 16 → 7 dice.
        Assert.Equal(7 * 3, P3Training.CreationHp(CharacterClass.Fighter, 16, Tables, new Script(2, 2, 2, 2, 2, 2, 2), Manual));
    }

    [Fact]
    public void D3023_LevelUp_RerollsHp_AndPaysSocialClassTimes256()
    {
        // Level 1 → 2, Fighter CON 10: 10 dice of rnd(3), all 1 → 10, not above the old max 10 → 11.
        var up = P3Training.LevelUp(Fighter(hpMax: 10), Tables, new Script(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), Manual);
        Assert.Equal(new P3LevelUp(2, 11, 1, 512), up);
        var dos = P3Training.LevelUp(Fighter(hpMax: 10), Tables, new Script(3, 3, 3, 3, 3, 3, 3, 3, 3, 3), Dos);
        Assert.Equal(new P3LevelUp(2, 30, 20, 0), dos);
        Assert.Equal(768, P3Training.StartingGold(3));
    }

    [Fact]
    public void SkillPick_MovesHalfWayToTheTarget_AndCanLower()
    {
        var s = new CharacterStats(15, 10, 15, 10, 10, 10); // Attack seed 2·15 + 30 = 60
        Assert.Equal(((110 + 100) >> 1) + 60 - 49, P3Training.SkillAfter(P3Skill.Attack, 100, 2, s, CharacterClass.Fighter, Race.Human, Tables));
        Assert.Equal(250, P3Training.SkillAfter(P3Skill.Attack, 250, 15, s, CharacterClass.Fighter, Race.Human, Tables));
        Assert.Equal(161, P3Training.SkillAfter(P3Skill.Attack, 200, 1, s, CharacterClass.Fighter, Race.Human, Tables)); // lowered
        Assert.Equal(61, P3Training.SkillAtCreation(P3Skill.Attack, s, CharacterClass.Fighter, Race.Human, Tables));
        // Fire Bow: no class table; seed 2·1 + 60 + 15 + 10 = 87.
        Assert.Equal(38, P3Training.SkillAtCreation(P3Skill.FireBow, s, CharacterClass.Fighter, Race.Human, Tables));
    }

    // ---- Town --------------------------------------------------------------------------------------------------------

    private static Party PartyOf(params int[] bank)
    {
        var p = new Party();
        foreach (var b in bank)
            p.Members.Add(new Character { Name = "M" + p.Members.Count, Status = CharacterStatus.Ok, GoldInBank = b });
        return p;
    }

    [Fact]
    public void D3025_Bank_ManualSharesWithAppleIiFee_NoFeeOption_DosEvenSplitAndWithdrawAll()
    {
        var m = PartyOf(0, 0, 0);
        m.GoldCarried = 600;
        Assert.Equal(TownTransactionKind.Ok, P3Town.DepositShares(m, [1, 2, 3], Manual).Kind);
        Assert.Equal([99, 199, 299], m.Members.Select(x => x.GoldInBank)); // ⌊S · 600 / 6.00001⌋: 3 gold lost
        Assert.Equal(0, m.GoldCarried);
        Assert.Equal(99, P3Town.AppleIiShareValue(600, 6));
        Assert.Equal(16, P3Town.AppleIiAccountShare(100, 6, 1)); // remainder 4 lost as well

        var n = PartyOf(0, 0, 0);
        n.GoldCarried = 600;
        Assert.Equal(TownTransactionKind.Ok, P3Town.DepositShares(n, [1, 2, 3], With("p3.bank", "no-fee")).Kind);
        Assert.Equal([100, 200, 300], n.Members.Select(x => x.GoldInBank));
        Assert.Equal(TownTransactionKind.Failed, P3Town.DepositShares(m, [1, 1, 1], Dos).Kind);
        Assert.False(P3Town.DosDeposit(m, 10, new Script(), Manual));

        var d = PartyOf(0, 0, 0);
        d.GoldCarried = 1000;
        Assert.False(P3Town.DosDeposit(d, 1001, new Script(), Dos));
        Assert.True(P3Town.DosDeposit(d, 101, new Script(3, 3), Dos)); // 33 each, remainder 2 to member 3
        Assert.Equal([33, 33, 35], d.Members.Select(x => x.GoldInBank));
        Assert.Equal(899, d.GoldCarried);
        Assert.True(P3Town.DosWithdrawAll(d, Dos));
        Assert.Equal(1000, d.GoldCarried);
        Assert.All(d.Members, x => Assert.Equal(0, x.GoldInBank));
        Assert.Equal(TownTransactionKind.Failed, P3Town.Withdraw(d, d.Members[0], 1, Dos).Kind);
    }

    [Fact]
    public void D3027_Trade_GiveAndSell_DosHasNone()
    {
        var a = new Character { Name = "A", InventoryItemIds = { 4, 22, 41, 64 } };
        var b = new Character { Name = "B", InventoryItemIds = { 1, 2, 3, 4, 5, 6, 7, 8 } };
        var empty = new Character { Name = "C" };
        Assert.Equal(P3TradeResult.NoTrade, P3Town.Give(a, 0, b, Dos));
        Assert.Equal(P3TradeResult.Done, P3Town.Give(a, 0, b, Manual));
        Assert.Equal(9, b.InventoryItemIds.Count);
        Assert.Equal(P3TradeResult.ReceiverFull, P3Town.Give(a, 0, b, Manual));
        Assert.Equal(P3TradeResult.NoSuchItem, P3Town.Give(a, 9, empty, Manual));
        Assert.Equal(P3TradeResult.SameMember, P3Town.Give(a, 0, a, Manual));
        var party = PartyOf(0);
        var town = new TownState(1, 0, "Pendragon", 150, new int[TownState.StockSlotCount]);
        var items = new ValueItems((22, 333), (41, 1));
        Assert.Equal(P3TradeResult.Done, P3Town.Sell(party, a, 0, town, items, Manual));
        Assert.Equal(374, party.GoldCarried); // ⌊⌊333 · 150 / 100⌋ · 75 / 100⌋ = ⌊499 · 0.75⌋
        Assert.Equal(1, town.GetStock(22));
        Assert.Equal([41, 64], a.InventoryItemIds);
        Assert.Equal(P3TradeResult.NoTrade, P3Town.Sell(party, a, 0, town, items, Dos));
        Assert.Equal(P3TradeResult.NoTrade, P3Town.Sell(party, a, 0, town, items, With("p3.trade", "give-only")));
        Assert.Equal(P3TradeResult.Done, P3Town.Give(a, 0, empty, With("p3.trade", "give-only")));
        Assert.Equal([41], empty.InventoryItemIds);
        Assert.Equal(1, P3Town.SalePrice(1, 100)); // at least 1
    }

    private sealed class ValueItems(params (int Id, int Value)[] items) : IItemCatalog
    {
        public ItemCatalogEntry? Get(int itemId) => items.Any(i => i.Id == itemId)
            ? new ItemCatalogEntry { Id = itemId, Name = "#" + itemId, ValueFactor = 0, Value = items.First(i => i.Id == itemId).Value }
            : null;

        public int Count => 181;
    }

    [Fact]
    public void D3033_ArmoryRestock_AppleIiRule_WithoutTheQuestTreasures()
    {
        var rule = new P3ArmoryRestockRule(2, 111, 3, 15, [66, 23, 172, 155, 8, 0], new HashSet<int> { 155, 172 });
        var stock = new int[TownState.StockSlotCount];
        stock[5] = 4;
        stock[23] = 5;
        stock[150] = 7;
        var town = new TownState(1, 0, "Pendragon", 150, stock);
        // Wipes: ids 5 and 110. Average level 2 → positions ⌊(1 − r₁r₂) · 6⌋: r₁r₂ = 0 → 6 (past the pool, nothing);
        // 0.5 · 0.5 → 4 (item 8); 0.75 · 2⁻²⁰ → 5 (item 0, nothing).
        const int half = 1 << 19, three4 = 3 << 18;
        P3ArmoryRestock.Apply(town, rule, 2, new Script(5, 110, 0, 0, half, half, three4, 1));
        Assert.Equal(0, town.GetStock(5));
        Assert.Equal(5, town.GetStock(23)); // set to 1 only when picked; never wiped here
        Assert.Equal(1, town.GetStock(8));
        Assert.Equal(7, town.GetStock(150)); // treasures are never wiped

        // A pick on the Light Key (position 2) or the Burnt Gem (3) stocks nothing.
        var t2 = new TownState(1, 0, "Pendragon", 150, new int[TownState.StockSlotCount]);
        P3ArmoryRestock.Apply(t2, rule with { Wipes = 0, Picks = 2 }, 1,
            new Script(half, half, 0, 0)); // level 1: ⌊0.75 · 3⌋ = 2 → 172; ⌊1 · 3⌋ = 3 → 155
        Assert.Equal(0, t2.GetStock(172));
        Assert.Equal(0, t2.GetStock(155));
    }

    [Fact]
    public void D3033_TravelTime_StepCostsByTerrain_AgingPer14Days()
    {
        Assert.Equal(1, P3TravelTime.StepCost(0));
        Assert.Equal(2, P3TravelTime.StepCost(P3TravelTime.Forest));
        Assert.Equal(3, P3TravelTime.StepCost(P3TravelTime.Hills));
        Assert.Equal(3, P3TravelTime.StepCost(P3TravelTime.Mountains));
        Assert.Equal(1, P3TravelTime.StepCost(P3Terrain.Water));
        Assert.Equal(13, P3TravelTime.DaysOut(41));
        Assert.Equal(0, P3TravelTime.AgeUnits(3 * 14 - 1));
        Assert.Equal(1, P3TravelTime.AgeUnits(3 * 14));
        Assert.Equal(2, P3TravelTime.AgeUnits(3 * 28 + 2));
    }
}
