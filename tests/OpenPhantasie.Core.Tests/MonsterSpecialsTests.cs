using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>Monster specials, OV2 <c>0x16a9</c> (<c>combat-resolution.md</c> §Monster specials, s26).</summary>
public class MonsterSpecialsTests
{
    /// <summary>Yields a fixed sequence of Next() results, then repeats the last.</summary>
    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count == 0)
                return minInclusive;
            var v = _values.Dequeue();
            if (_values.Count == 0)
                _values.Enqueue(v);
            return v;
        }
    }

    private static Character Member(string name, int hp = 40, int mpMax = 10) => new()
    {
        Name = name,
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        Skills = new CharacterSkills(80, 0, 0, 0, 0, 0, 0, 0),
        HpCur = hp,
        HpMax = hp,
        MagicMax = mpMax,
        MagicCur = mpMax,
        WeaponDamageA = 20,
    };

    private static Monster Caster(params int[] specials) => new()
    {
        Name = "CASTER",
        HitPoints = 10,
        CombatRating = 10,
        SpecialChance = 100,
        Specials = specials,
        AttackPower = 82,
        DamageField = 15,
    };

    private static (CombatEncounter enc, MonsterRank group) Setup(Monster mon, params Character[] members)
    {
        var party = new Party();
        party.Members.AddRange(members);
        var enc = new CombatEncounter(party);
        var group = enc.AddRank(mon, new Monster { Name = "BUDDY", HitPoints = 10 });
        enc.Prepare();
        return (enc, group);
    }

    [Theory]
    [InlineData(25, 40, 0, 1, true)]   // 40·2/3 = 26 → 26 ≤ 26
    [InlineData(26, 40, 0, 1, false)]
    [InlineData(47, 40, 2, 3, true)]   // third rank: 40·6/5 = 48 → 48 ≤ 48
    [InlineData(48, 40, 2, 3, false)]
    [InlineData(0, 0, 0, 1, false)]    // gate 0 never fires
    public void Gate_MatchesOv2_0d30(int roll, int gate, int g, int ranks, bool expected) =>
        Assert.Equal(expected, MonsterSpecials.GatePasses(roll, gate, g, ranks));

    [Theory]
    // Pick (0x1028): specials[rand·3/100] — 0..33 → first, 34..66 → second, 67..99 → third.
    [InlineData(0, 35)]
    [InlineData(33, 35)]
    [InlineData(34, 65)]
    [InlineData(66, 65)]
    [InlineData(67, 155)]
    [InlineData(99, 155)]
    public void Pick_IsRollTimesThreeOver100(int roll, int expectedAttackPower)
    {
        var mon = Caster(5, 6, 7);
        var (enc, group) = Setup(mon, Member("A"));
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(roll), new List<string>());
        Assert.All(group.Members, m => Assert.Equal(expectedAttackPower, m.AttackPower));
    }

    [Theory]
    [InlineData(5, 35)]
    [InlineData(6, 65)]
    [InlineData(7, 155)]
    [InlineData(8, 305)]
    public void Ids5To8_SetGroupAttackPower(int id, int expected)
    {
        var mon = Caster(id, id, id);
        var (enc, group) = Setup(mon, Member("A"));
        var r = MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0), new List<string>());
        Assert.Null(r);
        Assert.All(group.Members, m => Assert.Equal(expected, m.AttackPower));
    }

    [Theory]
    [InlineData(9, 9)]
    [InlineData(10, 14)]
    [InlineData(11, 21)]
    [InlineData(12, 32)]
    public void Ids9To12_SetGroupDamage(int id, int expected)
    {
        var mon = Caster(id, id, id);
        var (enc, group) = Setup(mon, Member("A"));
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0), new List<string>());
        Assert.All(group.Members, m => Assert.Equal(expected, m.DamageField));
    }

    [Theory]
    [InlineData(13, 8)]
    [InlineData(16, 20)]
    public void Ids13To16_SetGroupArmor(int id, int expected)
    {
        var mon = Caster(id, id, id);
        var (enc, group) = Setup(mon, Member("A"));
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0), new List<string>());
        Assert.All(group.Members, m => Assert.Equal(expected, m.Armor));
    }

    [Fact]
    public void Id3_HitsOneNonDeadMember_NoArmorNoResist()
    {
        var mon = Caster(3, 3, 3);
        var dead = Member("DEAD");
        dead.Status = CharacterStatus.Dead;
        dead.HpCur = 0;
        var a = Member("A");
        var b = Member("B");
        b.Status = CharacterStatus.Sleep; // sleepers are in the living list (0x43ae)
        var (enc, group) = Setup(mon, dead, a, b);
        enc.Prep[2].ArmorEff = 50;

        // index roll 0 (pick), target 60·2/100 = 1 → B, damage 50·70/100 = 35
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 60, 50), new List<string>());

        Assert.Equal(40, a.HpCur);
        Assert.Equal(5, b.HpCur);
    }

    [OriginalDataFact]
    public void Breath36_HitsEveryone_ArmorSubtracted_NoResistRoll()
    {
        var mon = Caster(36, 36, 36);
        var a = Member("A");
        var b = Member("B");
        var (enc, group) = Setup(mon, a, b);
        enc.Prep[0].ArmorEff = 5;
        enc.Prep[1].ArmorEff = 30;
        var log = new List<string>();

        // pick 0, then one scaled roll per member: 99·200/100 = 198 → 19 − armor
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 99, 99), log);

        Assert.Equal(40 - 14, a.HpCur);
        Assert.Equal(40 - 1, b.HpCur); // floor 1
        Assert.Contains("CASTER BREATHES", log);
    }

    [OriginalDataFact]
    public void Blast25_ResistRollUsesCvAgainstMpMax()
    {
        var mon = Caster(25, 25, 25); // CV 10
        var a = Member("A", mpMax: 10); // chance (10 − 10)·5 + 50 = 50 (OV2 0x1af0 reads @16 MP max, not ceiling @14)
        a.MpCeiling = 30;               // s33: ceiling ≠ max — the roll must ignore @14
        var b = Member("B", mpMax: 10);
        var (enc, group) = Setup(mon, a, b);
        var log = new List<string>();

        // pick 0; A: scaled 99 (→ 59 → 5), resist 49 < 50 → hit; B: scaled 99, resist 50 → resisted
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 99, 49, 99, 50), log);

        Assert.Equal(35, a.HpCur);
        Assert.Equal(40, b.HpCur);
        Assert.Contains("CASTER CASTS A SPELL", log);
    }

    [Fact]
    public void Sleep33_SleepsAffectedMembers_ClearsAction()
    {
        var mon = Caster(33, 33, 33);
        var a = Member("A");
        var (enc, group) = Setup(mon, a);
        enc.SetAction(0, CombatAction.Thrust(0));

        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 0, 0), new List<string>());

        Assert.Equal(CharacterStatus.Sleep, a.Status);
        Assert.Equal(CombatAction.Cleared, enc.Prep[0].PackedAction);
        Assert.False(enc.PartyWiped); // SLEEP counts as alive (0x43ae)
    }

    [Fact]
    public void ArmorReduced17_LowersArmorNotWeaponDamage_D016()
    {
        var mon = Caster(17, 17, 17); // power 8
        var a = Member("A");
        var (enc, group) = Setup(mon, a);
        enc.Prep[0].ArmorEff = 10;
        var weapon = enc.Prep[0].WeaponDamageEff;

        // pick 0; scaled 50·8/100 = 4; resist 0 → affected; armor (10·4 + 5)/10 = 4
        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 50, 0), new List<string>());

        Assert.Equal(4, enc.Prep[0].ArmorEff);
        Assert.Equal(weapon, enc.Prep[0].WeaponDamageEff);
    }

    [Fact]
    public void Slowed21_ScalesWeaponHit()
    {
        var mon = Caster(21, 21, 21); // power 8
        var a = Member("A");
        var (enc, group) = Setup(mon, a);

        MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0, 50, 0), new List<string>());

        Assert.Equal((80 * 4 + 5) / 10, enc.Prep[0].WeaponHitEff);
    }

    [Fact]
    public void Ids34And35_FallThroughToMeleeWithRider()
    {
        var mon = Caster(34, 35, 35);
        var (enc, group) = Setup(mon, Member("A"));
        Assert.Equal(MonsterMeleeRider.StunningBlow,
            MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(0), new List<string>()));
        Assert.Equal(MonsterMeleeRider.MagicDrain,
            MonsterSpecials.Use(enc, group, mon, new ScriptedRandomSource(99), new List<string>()));
    }

    [OriginalDataFact]
    public void ResolveRound_DragonBreathesThenMelees()
    {
        var dragon = Caster(36, 36, 36);
        var hero = Member("HERO");
        var party = new Party();
        party.Members.Add(hero);
        var enc = new CombatEncounter(party);
        enc.AddRank(dragon);
        enc.Prepare();

        // ready 0, gate 0, pick 0 → 36, breath scaled 50 (100 → 10 − armor 0 = 10),
        // melee target 0, hit 0, damage 50 (7), then 99s
        var rng = new ScriptedRandomSource(0, 0, 0, 50, 0, 0, 50, 99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.Contains("CASTER BREATHES", result.Log);
        Assert.Contains("CASTER HITS", result.Log); // [D✓] s65: no target name
        Assert.Equal(40 - 10 - 7, hero.HpCur);
    }

    [OriginalDataFact]
    public void ResolveRound_DeathByBreathLowersCon_D009()
    {
        // Breath 10 kills a 5-HP hero; CON 10, Luck (@10) 10 < 11 → −2 (OV2 0x12cf).
        var dragon = Caster(36, 36, 36);
        var hero = Member("HERO", hp: 5);
        var party = new Party();
        party.Members.Add(hero);
        var enc = new CombatEncounter(party);
        enc.AddRank(dragon);
        enc.Prepare();

        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0, 0, 0, 50, 99));

        Assert.Contains("HERO DIES", result.Log);
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(0, hero.HpCur);
        Assert.Equal(8, hero.Stats.Con);
    }

    [Fact]
    public void MagicDrain_KeepsSixTenths()
    {
        var a = Member("A", mpMax: 10);
        MonsterSpecials.ApplyRider(MonsterMeleeRider.MagicDrain, a, new CombatantPrep(), new List<string>());
        Assert.Equal(6, a.MagicCur); // (60 + 5)/10
    }

    [Fact]
    public void StunningBlow_DoesNotRaiseTheDead_D015()
    {
        var a = Member("A");
        a.Status = CharacterStatus.Dead;
        MonsterSpecials.ApplyRider(MonsterMeleeRider.StunningBlow, a, new CombatantPrep(), new List<string>());
        Assert.Equal(CharacterStatus.Dead, a.Status);
    }

    [OriginalDataFact]
    public void ResolveRound_WightStunsSurvivingTarget()
    {
        var wight = Caster(34, 34, 34);
        var hero = Member("HERO");
        var party = new Party();
        party.Members.Add(hero);
        var enc = new CombatEncounter(party);
        enc.AddRank(wight);
        enc.Prepare();

        // monster ready 0, gate 0 (1 ≤ 100·2/3), pick 0 → 34, target 0, hit 0, damage 50 (isqrt 7 · 15/15 = 7),
        // then 99s: the stunned hero doesn't wake in passes 3 and 5
        var rng = new ScriptedRandomSource(0, 0, 0, 0, 0, 50, 99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.Equal(33, hero.HpCur);
        Assert.Equal(CharacterStatus.Sleep, hero.Status);
        Assert.Contains("CASTER CASTS A SPELL", result.Log);
        Assert.Contains("A STUNNING BLOW", result.Log);
        Assert.False(result.PartyWiped);
    }
}
