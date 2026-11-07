using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's 7 × 7 battle (T6.3, <see cref="P4Battle"/>; spec <c>combat.md</c>): the field and monster pick
/// (<c>_e_terrain</c>), the groups (<c>_enc_mons</c>), placement and exits, moves and orders, the round, damage and the
/// D4 options the fight touches, the end of the fight and the campaign's hand-over.
/// </summary>
public class Phantasie4BattleTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();

    /// <summary>Every draw returns the lowest value (rand(n) = 1: every roll "succeeds").</summary>
    private sealed class Low : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>Every draw returns the highest value (rand(n) = n: every roll fails).</summary>
    private sealed class High : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    /// <summary>The lowest value, except <see cref="Result"/> (1..n) for a draw of rand(<see cref="Size"/>).</summary>
    private sealed class LowBut : IRandomSource
    {
        public int Size { get; set; } = -1;

        public int Result { get; set; } = 1;

        public int Next(int minInclusive, int maxExclusive) =>
            maxExclusive - minInclusive == Size ? minInclusive + Result - 1 : minInclusive;
    }

    /// <summary>The scripted results (1..n) in order, then the lowest value.</summary>
    private sealed class Script(params int[] results) : IRandomSource
    {
        private readonly Queue<int> _q = new(results);

        public int Next(int minInclusive, int maxExclusive) =>
            _q.Count > 0 ? Math.Clamp(_q.Dequeue(), minInclusive, maxExclusive - 1) : minInclusive;
    }

    private const int Skeleton = 0;

    private static P4Party Party(int members = 2, int level = 5, int cls = P4Ids.Fighter)
    {
        var p = new P4Party { Gold = 5000, AverageLevel = level };
        for (var i = 0; i < members; i++)
        {
            var m = new P4Member
            {
                Name = $"M{i}", Race = P4Ids.Human, Class = cls, Level = level, HpMax = 60, Hp = 60, MpMax = 10, Mp = 10,
                Abilities = [15, 15, 15, 15, 15, 15],
            };
            Array.Fill(m.Skills, 60);
            m.Items[0] = 0x1C + 4; // a melee weapon
            m.MoveBase = P4Creation.Stride(Manual, T.Creation, m.Race, m.Class); // +29 from creation
            m.MeleeBonus = P4TownRules.MeleeBonus(m); // +3B as the last town exit wrote it
            p.Members.Add(m);
        }
        return p;
    }

    private static P4Battle Dungeon(P4Party party, IRandomSource rng, int type = Skeleton, GameOptions? o = null) =>
        P4Battle.Dungeon(o ?? Manual, T, B, party, rng, type);

    // --- Data ----------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void BattleData_HoldsTheDecodedTables()
    {
        Assert.Equal(84, B.Monsters.Count);
        Assert.Equal([3, 2, 4, 1, 5, 0, 6], B.Tables.PlacementColumns);
        Assert.Equal(24, B.Tables.EdgeSquares.Count);
        Assert.Equal(14, B.Tables.SideExits.Count);
        Assert.Equal("ship", B.Tables.FieldFor(0)!.Name);
        Assert.Equal("hill", B.Tables.FieldFor(4)!.Name);
        Assert.Equal("mist", B.Tables.FieldFor(12)!.Name);
        Assert.Null(B.Tables.FieldFor(7)); // lava: no _e_on_* routine
        Assert.Equal(3, B.Tables.MaxGroups);
        Assert.Equal(P4ItemKind.Bow, B.Item(64)!.Kind);
        // Every follow-up group names an existing monster (FIGHT / POOLADD operands in O_001…O_021 are 0–83 as well).
        Assert.All(B.Monsters.Where(m => m.NextGroupChance > 0).SelectMany(m => m.Companions),
            c => Assert.InRange(c, 0, B.Monsters.Count - 1));
        Assert.All(B.Monsters, m => Assert.Equal(3, m.SpellIds.Count));
    }

    [OriginalDataTheory]
    [InlineData("desert", 2, new[] { 3 }, 34)]        // rand(4) − 1 + 12 × 2 + 8
    [InlineData("forest", 1, new[] { 10 }, 21)]       // rand(10) − 1 + 12
    [InlineData("mist", 5, new[] { 5, 50 }, 64)]      // rand(12) − 1 + 60, no override (rand(100) = 50)
    [InlineData("mist", 5, new[] { 5, 49, 2 }, 61)]   // override: rand(2) − 1 + 60
    [InlineData("ship", 0, new[] { 1, 64, 1 }, 76)]   // Swashbuckler on rand(100) < 65
    [InlineData("ship", 0, new[] { 1, 65 }, 75)]      // else Pirate
    public void OutdoorMonsterPick_IsTheFieldsFormula(string field, int area, int[] rolls, int expected)
    {
        var f = B.Tables.Fields.Single(x => x.Name == field);
        Assert.Equal(expected, P4Battle.FirstMonster(f.Monster, area, new Script(rolls)));
    }

    [OriginalDataFact]
    public void EveryOutdoorEncounterSquare_PicksAnExistingMonster()
    {
        var bad = new List<string>();
        foreach (var (n, map) in Atlas.Outdoors)
        {
            var area = T.Outdoor.AreaLevelByMap[n - 1];
            foreach (var type in map.Tiles.Select(t => P4OutdoorMap.TypeOf(t)).Distinct())
            {
                if (type >= T.Outdoor.EncounterPerMilleByType.Count || (T.Outdoor.EncounterPerMilleByType[type] ?? 0) == 0)
                    continue;
                var f = B.Tables.FieldFor(type);
                if (f is null)
                {
                    bad.Add($"map {n} type {type}: no field");
                    continue;
                }
                var max = f.Monster.Die - 1 + f.Monster.AreaLevelTimes * area + f.Monster.Add;
                max = Math.Max(max, f.Monster.OverrideDie - 1 + f.Monster.OverrideAdd);
                if (max >= B.Monsters.Count)
                    bad.Add($"map {n} type {type}: up to monster {max}");
            }
        }
        Assert.Empty(bad);
    }

    // --- Setup ---------------------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(1, 1)]  // group max 6 > level 1 + 1 → n = the level
    [InlineData(4, 4)]  // 6 > 5 → n = 4
    [InlineData(5, 6)]  // 6 ≤ 6 → n = 6
    public void GroupSize_IsCappedByTheAverageLevel(int level, int largest)
    {
        var b = Dungeon(Party(level: level), new High());
        Assert.Single(b.Groups); // rand(100) = 100 > +34: no second group
        Assert.Equal(largest, b.Groups[0].Count);
        Assert.Equal(largest, b.Monsters.Count);
        Assert.Equal(B.Monsters[Skeleton].Gold * largest, b.Gold);
    }

    [OriginalDataFact]
    public void FollowGroups_AreAtMostThree_AndTheLevelIsTheirMean()
    {
        // Skeleton: next group 90 %, companions 0 / 0: rand(100) = 1 always follows, up to three groups.
        var b = Dungeon(Party(level: 9), new Low());
        Assert.Equal(3, b.Groups.Count);
        Assert.Equal(Math.Max(1, B.Monsters[Skeleton].Level), b.EncounterLevel);
        Assert.Equal(B.Monsters[Skeleton].Nice, b.EncounterNice);
    }

    [OriginalDataFact]
    public void Placement_MembersBottomRightUp_MonstersTopRowCentreOut()
    {
        var b = Dungeon(Party(members: 4), new High());
        Assert.Equal([45, 46, 44, 47], b.Members.Select(m => m.Square));
        Assert.Equal([3, 2, 4, 1, 5, 0], b.Monsters.Select(m => m.Square));
    }

    [OriginalDataFact]
    public void Exits_DungeonHasOneSideExit_OutdoorsEveryEdge()
    {
        var d = Dungeon(Party(), new High());
        Assert.Single(d.Exits);
        Assert.Contains(d.Exits.Single(), B.Tables.SideExits.Select(e => e.Square));
        var o = P4Battle.Outdoor(Manual, T, B, Party(), new SeededRandomSource(3), new P4OutdoorFight(false, false, false, false, 1), 6);
        Assert.Equal(24, o.Exits.Count);
        Assert.Equal("grass", o.Field);
    }

    [OriginalDataFact]
    public void Surprise_PartySurprises_MonstersStartActed()
    {
        var o = P4Battle.Outdoor(Manual, T, B, Party(), new SeededRandomSource(3), new P4OutdoorFight(true, false, false, false, 1), 6);
        Assert.All(o.Monsters, m => Assert.Equal(P4BattleMonster.Acted, m.State));
        Assert.Equal(P4BattlePrompt.Options, o.Prompt);
    }

    [OriginalDataFact]
    public void Surprise_MonstersSurprise_RunTheFirstRoundBeforeAnyOrder()
    {
        var o = P4Battle.Outdoor(Manual, T, B, Party(), new SeededRandomSource(3), new P4OutdoorFight(false, true, false, false, 1), 6);
        Assert.Equal(1, o.Round);
        Assert.Equal(P4BattlePrompt.Options, o.Prompt);
    }

    [OriginalDataFact]
    public void SetUp_EquipsTheBestUsableItems_AndTheMeleeBonus()
    {
        var p = Party(members: 1);
        var m = p.Members[0];
        m.Items = [1, 9, 0x1C, 0x40, 0x4C, 0, 0, 0, 0]; // Wooden Shield, Clothing, Knife, Self Bow, a thrown weapon
        var b = Dungeon(p, new High());
        var bm = b.Members[0];
        Assert.Equal((1, 9, 0x1C, 0x40, 0x4C), (bm.ShieldId, bm.ArmourId, bm.WeaponId, bm.BowId, bm.ThrownId));
        // +3B = trunc(trunc(15 / 6) + |3 − 3.5| × 5 / 11) = 2
        Assert.Equal(B.Item(0x1C)!.Points + 2, bm.Wm);
        Assert.Equal(B.Item(9)!.Points, bm.Armour);
        Assert.Equal(m.Skill(P4Skill.Attack), bm.Hm);
    }

    [OriginalDataFact]
    public void SetUp_ReadsTheRecordsMeleeBonusAndStride_NotTheirFormulas()
    {
        // _set1 adds the +3B _exittown wrote; _calmove / _baserate read +29 (a Motion Potion's +2 counts, a Duck keeps its
        // stride) [C].
        var p = Party(members: 1);
        var m = p.Members[0];
        m.Items = [0x1C, 0, 0, 0, 0, 0, 0, 0, 0];
        m.MeleeBonus = 7;
        m.MoveBase = 12;
        m.Abilities[0] = 3; // a Strength drop since the last town changes nothing
        var bm = Dungeon(p, new High()).Members[0];
        Assert.Equal(B.Item(0x1C)!.Points + 7, bm.Wm);
        Assert.Equal(12, bm.Stride);
        Assert.Equal(12, bm.BaseMove); // no armour, no Shoes of Speed
    }

    // --- Orders --------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Steps_DiagonalsCostOneThenTwo()
    {
        var b = Dungeon(Party(members: 1), new High());
        b.ChooseOption(P4EncounterOption.Fight);
        var m = b.Current!;
        var mv = m.MoveLeft;
        Assert.True(b.Step(5)); // NW: 1
        Assert.True(b.Step(5)); // NW: 2
        Assert.Equal(mv - 3, m.MoveLeft);
        Assert.True(b.Step(6)); // N: 1
        Assert.Equal(mv - 4, m.MoveLeft);
    }

    [OriginalDataFact]
    public void Steps_RefuseAnOccupiedSquare()
    {
        var b = Dungeon(Party(members: 2), new High());
        b.ChooseOption(P4EncounterOption.Fight);
        // M0 on 45, M1 on 46: east is taken.
        Assert.False(b.Step(4));
    }

    [OriginalDataFact]
    public void Offered_FighterNextToAMonster_GetsLungeThrustAimAttack()
    {
        var b = Dungeon(Party(members: 1), new High());
        b.Monsters[0].Square = 38; // next to 45
        b.ChooseOption(P4EncounterOption.Fight);
        var offered = b.Offered();
        Assert.Contains(P4ActionKind.Lunge, offered);
        Assert.Contains(P4ActionKind.Thrust, offered);
        Assert.Contains(P4ActionKind.AimBlow, offered);
        Assert.DoesNotContain(P4ActionKind.Attack, offered); // Hm 60 < 75
        Assert.Contains(P4ActionKind.Parry, offered);
        Assert.False(b.Order(new P4Order(P4ActionKind.Thrust, Target: 3))); // not in reach
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
    }

    [OriginalDataFact]
    public void Offered_ThiefReachesTwoSquares()
    {
        var b = Dungeon(Party(members: 1, cls: P4Ids.Thief), new High());
        b.Monsters[0].Square = 31; // two rows above 45
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Contains(P4ActionKind.Thrust, b.Offered());
        Assert.DoesNotContain(P4ActionKind.Lunge, b.Offered());
    }

    // --- Weapon actions and D4-042 -------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void Attack_StrikesTwiceUnderTheManual_OnceInTheCode(bool code, int misses)
    {
        var p = Party(members: 1);
        p.Members[0].Skills[(int)P4Skill.Attack] = 80;
        var b = Dungeon(p, new High(), o: code ? Code : Manual);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        b.Monsters[0].Square = 38;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Attack, Target: 38)));
        // High rng: the phase roll passes only in phase 5; every hit roll (rand(100) = 100) misses.
        var mine = b.Log.TakeWhile(l => !l.StartsWith(b.Monsters[0].Name, StringComparison.Ordinal));
        Assert.Equal(misses, mine.Count(l => l == GameText.S("p4x.0x44859")));
    }

    [OriginalDataFact]
    public void KillingTheLastMonster_WinsWithGoldAndThePool()
    {
        var p = Party(members: 1);
        var b = Dungeon(p, new Low());
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        var mon = b.Monsters[0];
        mon.Square = 38;
        mon.Hp = 1;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
        Assert.Equal(P4BattleEnd.AllKilled, b.End);
        Assert.Equal(P4FightOutcome.Won, b.Outcome);
        Assert.Equal(5000 + b.Gold, p.Gold);
        Assert.Equal(B.Monsters[Skeleton].Experience / 10, p.ExperiencePool);
        Assert.Equal(P4BattlePrompt.Over, b.Prompt);
        b.Continue();
        Assert.True(b.Done);
    }

    [OriginalDataTheory]
    [InlineData(false, 60, true)]  // manual: Spot Item 60 >= 50 shows the appraisal
    [InlineData(false, 40, false)] // manual: below the amount
    [InlineData(true, 60, false)]  // code: found items are never appraised
    public void D4_036_FoundItem_ComesWithItsAppraisal_ForAHighSpotItem(bool code, int spotItem, bool appraised)
    {
        var p = Party(members: 1);
        p.Members[0].Skills[(int)P4Skill.SpotItem] = spotItem;
        var rng = new LowBut();
        var b = Dungeon(p, rng, o: code ? Code : Manual);
        rng.Size = 2 * b.EncounterLevel + 6; // the extra-item roll picks entry 3 (the first non-empty one)
        rng.Result = 3;
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        b.Monsters[0].Square = 38;
        b.Monsters[0].Hp = 1;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
        Assert.Equal(P4BattlePrompt.Treasure, b.Prompt);
        var id = b.OfferedItem!.Value;
        var text = string.Concat(T.Service.IdentifyLines[id].Select(GameText.S)).Trim();
        Assert.Equal(appraised, b.Log[^1] == text);
        // s156: _ident_mi's text comes with _ident_mi's own sound (_sdmag on entry).
        Assert.Equal(appraised, p.Sounds.Since(0).Contains(P4Sounds.Identify));
    }

    [OriginalDataFact]
    public void FoundItem_FullPack_IsPassedOverWithoutTheQuestion()
    {
        // _findit (s140, static [C]): no free slot among the pack's 128 → no question, no message.
        var p = Party(members: 1);
        Array.Fill(p.Pack, 1);
        var rng = new LowBut();
        var b = Dungeon(p, rng);
        rng.Size = 2 * b.EncounterLevel + 6;
        rng.Result = 3;
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        b.Monsters[0].Square = 38;
        b.Monsters[0].Hp = 1;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
        Assert.Equal(P4BattlePrompt.Over, b.Prompt);
        Assert.Null(b.OfferedItem);
    }

    [OriginalDataTheory]
    [InlineData(72, 12)] // Lord Castlebaum
    [InlineData(73, 9)]  // Kor
    [InlineData(83, 59)] // Arathamus: ending flag f3
    public void EventFight_WonByKilling_SetsItsStoryFlag(int type, int flag)
    {
        // _monevf (ending.md §1, events.md §3) [C].
        var p = Party(members: 1);
        p.Members[0].Skills[(int)P4Skill.Attack] = 255; // a sure hit through Arathamus's defence
        var b = Dungeon(p, new Low(), type);
        Assert.Equal(flag, b.EventFlag);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        var mon = b.Monsters[0];
        mon.Square = 38;
        mon.Hp = 1;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Thrust, Target: 38)));
        Assert.Equal(P4BattleEnd.AllKilled, b.End);
        Assert.Equal(1, p.StoryFlags[flag]);
    }

    [OriginalDataTheory]
    [InlineData(6, true)] // Viper, HP base 20: 1 HP is below 30 %
    [InlineData(83, false)]
    public void LowHpMonsterOnAnExit_LeavesUnlessTheFightIsAnEventFight(int type, bool leaves)
    {
        // _b_chkflee: HP below 30 % of the HP base sends a monster to the exit, except while _evmon >= 0 [C].
        var p = Party(members: 1);
        var b = Dungeon(p, new High(), type);
        Assert.Equal(type == 83 ? 59 : null, b.EventFlag);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        var mon = b.Monsters[0];
        mon.Square = b.Exits.Single();
        mon.Hp = 1;
        b.Members[0].Square = 48 - mon.Square; // the opposite side of the field (no obstacles in a location map)
        Assert.True(P4Battle.Reach(b.Members[0].Square, mon.Square) > 1);
        b.ChooseOption(P4EncounterOption.Fight);
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(leaves, mon.Gone);
        Assert.All(Enumerable.Range(1, 63), f => Assert.Equal(0, p.StoryFlags[f]));
    }

    [Fact]
    public void AimedLocation_IsHeadThreeTimesInTwelve_ElseTorso()
    {
        var parts = Enumerable.Range(1, 12).Select(r => P4Body.AimedLocation(new Script(r))).ToList();
        Assert.Equal(3, parts.Count(p => p == P4BodyPart.Head));
        Assert.Equal(9, parts.Count(p => p == P4BodyPart.Torso));
    }

    [Theory]
    [InlineData(true, 1, P4BodyPart.Head)]
    [InlineData(true, 12, P4BodyPart.Torso)]
    [InlineData(false, 100, P4BodyPart.LeftLeg)] // not aimed: the melee roll, u = 0.99
    public void MonsterHitLocation_AnAimedBlowTakesTheHeadOrTorso(bool aimed, int roll, P4BodyPart part) =>
        Assert.Equal(part, P4Body.MonsterHitLocation(new Script(roll), 1, aimed));

    [Fact]
    public void MonsterHitLocation_AimedStillCannotReachAType5Monster() =>
        Assert.InRange((int)P4Body.MonsterHitLocation(new Script(3), 5, aimed: true), 2, 5);

    // --- Monster attacks and D4-043 ------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, false)] // manual: a lost head kills
    [InlineData(true, true)]   // code: the member stays alive, Out
    public void LostHead_KillsUnderTheManualOnly(bool code, bool alive)
    {
        var p = Party(members: 2);
        p.Members[0].Wounds[(int)P4BodyPart.Head] = P4Body.Lost;
        var b = Dungeon(p, new Low(), o: code ? Code : Manual);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        b.Monsters[0].Square = 38; // next to M0 (45), away from M1 (46)? 46 is diagonal-adjacent too: M0 is first by square
        b.Members[1].Square = 48;
        b.ChooseOption(P4EncounterOption.Fight);
        b.Order(new P4Order(P4ActionKind.Parry)); // M1 (M0 is Out and gives no orders)
        Assert.Equal(alive, p.Members[0].Alive);
        if (alive)
            Assert.Equal(4, p.Members[0].CombatState);
    }

    [OriginalDataFact]
    public void Parry_CutsTheFirstHitByTheShield()
    {
        var p = Party(members: 1);
        p.Members[0].Items[1] = 5; // Giant Shield (16) — needs Str 18
        p.Members[0].Abilities[0] = 18;
        var b = Dungeon(p, new Low());
        Assert.Equal(16, b.Members[0].ShieldValue);
    }

    // --- Spells and their options ----------------------------------------------------------------------------------------

    private static P4Battle Caster(int spell, GameOptions o, out P4Party p)
    {
        p = Party(members: 1, cls: P4Ids.Priest);
        p.Members[0].Spells[spell - 1] = 1;
        var b = Dungeon(p, new Low(), o: o);
        b.ChooseOption(P4EncounterOption.Fight);
        return b;
    }

    [OriginalDataTheory]
    [InlineData(false, 1)] // manual Slow 1 costs 1
    [InlineData(true, 2)]  // the code's table: 2
    public void MpCosts_FollowTheOption(bool code, int cost)
    {
        var b = Caster(11, code ? Code : Manual, out var p);
        Assert.Contains(11, b.CastableSpells());
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 11)));
        Assert.Equal(10 - cost, p.Members[0].Mp);
    }

    [OriginalDataFact]
    public void Casting_SetsTheInnMpGrowthFlag()
    {
        // _psetcast: every spell cast in combat sets +72; the next inn stay reads it (training.md §4.2).
        var b = Caster(11, Code, out var p);
        Assert.False(p.Members[0].MpGrowth);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 11)));
        Assert.True(p.Members[0].MpGrowth);
    }

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Weakness_ManualLowersDamage_CodeLowersDefence(bool code)
    {
        var b = Caster(30, code ? Code : Manual, out _);
        var g = b.Groups[0];
        var defence = g.Defence;
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 30)));
        // Low rng: one Skeleton per group; each group's record is hit once.
        if (code)
        {
            Assert.Equal((int)Math.Truncate(defence * 0.8), g.Defence);
            Assert.Equal(1.0, g.DamageFactor);
        }
        else
        {
            Assert.Equal(defence, g.Defence);
            Assert.Equal(0.8, g.DamageFactor, 6);
        }
    }

    [OriginalDataFact]
    public void Healing_TargetsAMember_AndHealsWounds()
    {
        var b = Caster(2, Manual, out var p);
        foreach (var x in b.Monsters)
            x.State = P4BattleMonster.Asleep; // they wake (Low rng) but do nothing this round
        var m = p.Members[0];
        m.Hp = 10;
        m.Wounds[(int)P4BodyPart.LeftLeg] = P4Body.Serious;
        Assert.False(b.Order(new P4Order(P4ActionKind.Cast, Spell: 2))); // needs a member
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Target: 0, Spell: 2)));
        Assert.Equal(11, m.Hp); // manual 1…27, Low rng → 1
        Assert.Equal(0, m.Wounds[(int)P4BodyPart.LeftLeg]);
    }

    [OriginalDataTheory]
    [InlineData(false, P4BattleEnd.None)] // manual: Dissolve melts over three rounds
    [InlineData(true, P4BattleEnd.Melted)]
    public void Dissolve_CodeMeltsAtOnce_ManualOverThreeRounds(bool code, P4BattleEnd end)
    {
        var p = Party(members: 1, level: 1, cls: P4Ids.Wizard);
        p.Members[0].Spells[47] = 1;
        var b = Dungeon(p, new Low(), type: 1, o: code ? Code : Manual); // Undead Goblin: HP 13 base
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 48)));
        Assert.Equal(end, b.End);
        if (!code)
            Assert.All(b.Monsters.Where(m => m.OnField), m => Assert.Single(m.Dissolving));
    }

    // --- Options ---------------------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(true, 5000 % 256, 1)]
    [InlineData(false, 0, 0)]
    public void BegForMercy_GoldAndGoods(bool code, long gold, int itemsLeft)
    {
        var p = Party(members: 1);
        p.Members[0].Items[1] = 85; // a potion: unequipped
        var b = Dungeon(p, new Low(), o: code ? Code : Manual);
        b.ChooseOption(P4EncounterOption.Beg);
        Assert.Equal(P4BattleEnd.Begged, b.End);
        Assert.Equal(P4FightOutcome.NotWon, b.Outcome);
        Assert.Equal(gold, p.Gold);
        Assert.Equal(itemsLeft, p.Members[0].Items.Count(i => i == 85));
        Assert.Equal(0, p.ExperiencePool);
    }

    [OriginalDataFact]
    public void Greet_Accepted_EndsWithoutGold()
    {
        // _enc_opt: accepted when rand(100) × (1 − _emcham) > 90 − nice in a dungeon (Undead Goblin, nice −2: 100 > 92).
        var p = Party();
        var b = Dungeon(p, new High(), type: 1);
        b.ChooseOption(P4EncounterOption.Greet);
        Assert.Equal(P4BattleEnd.Greeted, b.End);
        Assert.Equal(5000, p.Gold);
    }

    [OriginalDataFact]
    public void Greet_Refused_RunsTheRoundAndAsksAgain()
    {
        var b = Dungeon(Party(), new Script(1), type: 1); // rand(100) = 1, not above 92
        b.ChooseOption(P4EncounterOption.Greet);
        Assert.Equal(P4BattleEnd.None, b.End);
        // _enc_opt: a refusal goes on to the members' orders, then the round.
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        while (b.Prompt == P4BattlePrompt.Order)
            b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(1, b.Round);
        Assert.Equal(P4BattlePrompt.Options, b.Prompt);
    }

    [OriginalDataFact]
    public void Greet_FailsOnceAMonsterIsHurt_UnlessTheLimitIsBelowZero()
    {
        // Skeleton (nice −10): 100 > 100 fails even before any damage; the undead never accept a greeting in a dungeon.
        Assert.Equal(P4BattleEnd.None, GreetWith(type: 0, hurt: false));
        Assert.Equal(P4BattleEnd.Greeted, GreetWith(type: 1, hurt: false));
        Assert.Equal(P4BattleEnd.None, GreetWith(type: 1, hurt: true)); // _emcham: 0 > 92 fails

        static P4BattleEnd GreetWith(int type, bool hurt)
        {
            var b = Dungeon(Party(members: 4, level: 8), new High(), type: type);
            if (hurt) // as after any damage to a monster (MonsterDamage sets it)
                typeof(P4Battle).GetProperty(nameof(P4Battle.MonsterHurt))!.SetValue(b, true);
            b.ChooseOption(P4EncounterOption.Greet);
            return b.End;
        }
    }

    [OriginalDataFact]
    public void Flee_MembersOnTheExitLeave()
    {
        var p = Party(members: 1);
        var b = Dungeon(p, new High());
        b.Members[0].Square = b.Exits.Single();
        b.ChooseOption(P4EncounterOption.Flee);
        Assert.Equal(P4BattleEnd.Fled, b.End);
        Assert.Equal(P4FightOutcome.NotWon, b.Outcome);
    }

    // --- Whole fights ------------------------------------------------------------------------------------------------------

    /// <summary>A simple player: Fight; each member walks toward the nearest monster and strikes or shoots; takes all items.</summary>
    private static void Play(P4Battle b, int maxRounds = 60)
    {
        for (var guard = 0; guard < 5000 && !b.Done; guard++)
        {
            switch (b.Prompt)
            {
                case P4BattlePrompt.Options:
                    if (b.Round >= maxRounds)
                        return;
                    // A field the greedy walk cannot cross (mist obstacles): give up and run for the edge.
                    b.ChooseOption(b.Round >= maxRounds / 2 ? P4EncounterOption.Flee : P4EncounterOption.Fight);
                    break;
                case P4BattlePrompt.Order:
                {
                    var m = b.Current!;
                    var foes = b.Monsters.Where(x => x.OnField).ToList();
                    for (var steps = 0; steps < 20 && foes.Count > 0; steps++)
                    {
                        var near = foes.OrderBy(x => P4Battle.Range2(m.Square, x.Square)).First();
                        if (P4Battle.Reach(m.Square, near.Square) <= 1)
                            break;
                        var dirs = Enumerable.Range(0, 8)
                            .Where(d => P4Battle.Neighbour(m.Square, d) is int n
                                && P4Battle.Range2(n, near.Square) < P4Battle.Range2(m.Square, near.Square))
                            .ToList();
                        if (!dirs.Any(b.Step))
                            break;
                    }
                    var offered = b.Offered();
                    var target = foes.OrderBy(x => P4Battle.Range2(m.Square, x.Square)).FirstOrDefault();
                    var done = false;
                    foreach (var k in new[] { P4ActionKind.Thrust, P4ActionKind.FireBow, P4ActionKind.Throw })
                    {
                        if (!done && target is not null && offered.Contains(k))
                            done = b.Order(new P4Order(k, target.Square));
                    }
                    if (!done)
                        b.Order(new P4Order(P4ActionKind.Parry));
                    break;
                }
                case P4BattlePrompt.Treasure:
                    b.TakeItem(true);
                    break;
                case P4BattlePrompt.Over:
                    b.Continue();
                    break;
            }
        }
    }

    [OriginalDataTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SeededFights_RunToAnEnd(int seed)
    {
        foreach (var o in new[] { Manual, Code })
        {
            var p = Party(members: 4, level: 8);
            var b = Dungeon(p, new SeededRandomSource(seed), type: seed * 7 % 40, o: o);
            Play(b);
            Assert.True(b.Done, $"seed {seed}: still fighting after 60 rounds");
            Assert.Equal(b.Outcome == P4FightOutcome.Lost, !p.AnyAlive);
        }
    }

    [OriginalDataFact]
    public void SeededOutdoorFights_OnEveryField_RunToAnEnd()
    {
        foreach (var f in B.Tables.Fields)
        {
            for (var seed = 1; seed <= 3; seed++)
            {
                var p = Party(members: 4, level: 8);
                var b = P4Battle.Outdoor(Manual, T, B, p, new SeededRandomSource(seed),
                    new P4OutdoorFight(false, false, f.Name == "ship", false, 1), f.TerrainTypes[0]);
                Play(b);
                var onField = b.Members.Where(m => m.OnField).Select(m => m.Square)
                    .Concat(b.Monsters.Where(m => m.OnField).Select(m => m.Square)).ToList();
                Assert.Equal(onField.Count, onField.Distinct().Count());
                // Mist and the Netherworld can wall the sides off for this test's greedy walk (and the monsters' [~] walk).
                var blocking = Enumerable.Range(0, P4Battle.Squares).Count(q => !b.Walkable(q));
                Assert.True(b.Done || blocking > 8, $"{f.Name} seed {seed}: round {b.Round} {b.Prompt} {b.End} monsters "
                    + string.Join(",", b.Monsters.Select(m => $"{m.Name}@{m.Square}/{m.Hp}/{m.State}/{m.Dead}/{m.Gone}"))
                    + " members " + string.Join(",", b.Members.Select(m => $"{m.Square}/{m.Member.Hp}/{m.State}"))
                    + " log " + string.Join("|", b.Log));
            }
        }
    }

    // --- Campaign ------------------------------------------------------------------------------------------------------

    /// <summary>The first outdoor square (from map 1) whose 3 × 3 block is all grassland.</summary>
    private static P4Location Grass()
    {
        foreach (var (n, m) in Atlas.Outdoors.OrderBy(e => e.Key))
            for (var y = 2; y < 24; y++)
                for (var x = 2; x < 24; x++)
                    if (Enumerable.Range(-1, 3).All(dy => Enumerable.Range(-1, 3)
                            .All(dx => P4OutdoorMap.TypeOf(m.Tile(x + dx, y + dy)) == 6)))
                        return new P4Location(P4Place.Outdoors, n, x, y);
        throw new InvalidOperationException("no grass");
    }

    [OriginalDataFact]
    public void Campaign_StartsTheBattleAtAFight_AndHandsTheOutcomeBack()
    {
        var files = Phantasie4World.LoadDungeonFiles();
        var party = Party(members: 4, level: 8);
        var rng = new SeededRandomSource(7);
        var c = new Phantasie4Campaign(Manual, T, Atlas, party, rng, n => files[n], id => $"#{id}",
            Grass(), B);
        c.Start();
        var fights = 0;
        for (var step = 0; step < 3000 && fights < 3 && !c.PartyLost; step++)
        {
            if (c.Battle is { } battle)
            {
                Assert.Throws<InvalidOperationException>(() => c.Capture());
                fights++;
                Play(battle);
                if (!battle.Done)
                    return; // a long fight: the hand-over is what this test checks
                c.Follow();
                Assert.Null(c.Battle);
                Assert.NotEqual(P4OutdoorPrompt.Fight, c.Outdoor.Prompt);
                continue;
            }
            switch (c.Place)
            {
                case P4Place.Outdoors:
                    switch (c.Outdoor.Prompt)
                    {
                        case P4OutdoorPrompt.Action: c.Outdoor.Move(step % 2 == 0 ? 4 : 3); break; // east, west: stay on the grass
                        case P4OutdoorPrompt.Wait: c.Outdoor.Continue(); break;
                        case P4OutdoorPrompt.Confirm: c.Outdoor.Answer(false); break;
                        default: return;
                    }
                    break;
                default:
                    return;
            }
            c.Follow();
        }
        Assert.True(fights > 0, $"no fight met: {c.Place} {c.Outdoor.Prompt} {c.Location} lost {c.PartyLost} " + string.Join("|", c.Outdoor.Box));
    }
}
