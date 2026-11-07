using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class CombatResolutionTests
{
    /// <summary>Yields a fixed sequence of Next() results, then repeats the last.</summary>
    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values) =>
            _values = new Queue<int>(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count == 0)
                return minInclusive;
            var v = _values.Dequeue();
            if (_values.Count == 0)
                _values.Enqueue(v); // keep last for further draws
            return v;
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)] // DOS quirk: not floor(sqrt(1))
    [InlineData(2, 1)]
    [InlineData(3, 2)] // DOS quirk: Newton stops at 2, not floor(sqrt(3))=1
    [InlineData(4, 2)]
    [InlineData(8, 3)] // DOS quirk vs floor(sqrt(8))=2
    [InlineData(9, 3)]
    [InlineData(15, 3)]
    [InlineData(16, 4)]
    [InlineData(99, 9)]
    [InlineData(100, 10)]
    public void IntegerSqrt_MatchesFun3675Quirks(int n, int expected) =>
        Assert.Equal(expected, CombatMath.IntegerSqrt(n));

    [Fact]
    public void DefenseRating_MatchesCombatPrepFormula()
    {
        // DEX=10, armor=2, parry=40, shield=6
        // (10*20 + 2*-50 + (40*10)/4) / 10 = (200 - 100 + 100) / 10 = 20
        // + shield/3 = 20 + 2 = 22
        Assert.Equal(22, CombatMath.ComputeDefenseRating(10, 2, 40, 6));
    }

    [Fact]
    public void DefenseRating_ClampsNegativeToZeroThenAddsShield()
    {
        // Heavy armor can drive the pre-shield term negative.
        var def = CombatMath.ComputeDefenseRating(dexterity: 1, armorSoakBase: 10, parrySkill: 0, shieldSoak: 9);
        // (20 + -500 + 0)/10 = -48 → 0; + 9/3 = 3
        Assert.Equal(3, def);
    }

    [Theory]
    [InlineData(15, 12, 65)]  // (15-12)*5+50
    [InlineData(10, 10, 50)]
    [InlineData(5, 20, 0 - 25)] // unclamped: -25
    public void CvHitChance_Unclamped(int atk, int def, int expected) =>
        Assert.Equal(expected, CombatMath.CvHitChance(atk, def));

    [Theory]
    [InlineData(40, 2, 5, 15)] // 40/2 - 5
    [InlineData(40, 4, 5, 5)]  // SLASH more swings → lower chance
    [InlineData(10, 1, 20, -10)]
    public void PartyMeleeHitChance_DividesBySwings(int hit, int swings, int monDef, int expected) =>
        Assert.Equal(expected, CombatMath.PartyMeleeHitChance(hit, swings, monDef));

    [Fact]
    public void MonsterMelee_HitUsesRollTimes6VsMetric()
    {
        var metric = CombatMath.MonsterMeleeMetric(monsterAttackPower: 80, swings: 1, partyDefense: 10);
        // (80/1 - 10)*10 = 700
        Assert.Equal(700, metric);
        Assert.True(CombatMath.MonsterMeleeHits(roll0To99: 50, metric)); // 300 < 700
        Assert.False(CombatMath.MonsterMeleeHits(roll0To99: 99, metric: 500)); // 594 < 500? false
    }

    [Fact]
    public void MonsterToPartyDamage_AppliesIsqrtAndArmorAndParry()
    {
        // roll 16 → coef 4; dmg field 15 → (4*15)/15 - 2 = 4-2 = 2; parry soak 3 → -1
        var dmg = CombatMath.MonsterToPartyDamage(
            roll0To99: 16,
            monsterDamageField: 15,
            partyArmorEff: 2,
            isParrying: true,
            shieldSoak: 3,
            out var consumed);
        Assert.True(consumed);
        Assert.Equal(-1, dmg);
        Assert.Equal(1, CombatMath.ApplyDamageFloor(dmg));
    }

    [Theory] // D-011 (manual): no skill gate; 3 swings, 4 from @250 >= 225 (= DOS @250/75+1 where DOS allows SLASH)
    [InlineData(0, 3)]
    [InlineData(22, 3)]
    [InlineData(149, 3)]
    [InlineData(150, 3)]
    [InlineData(224, 3)]
    [InlineData(225, 4)]
    [InlineData(255, 4)]
    public void SlashSwings_FollowManualWithDosCount(int weaponHitEff, int swings)
    {
        Assert.Equal(swings, CombatAction.SlashSwings(weaponHitEff));
        Assert.Equal(swings * 10 + 2, CombatAction.SlashFor(weaponHitEff, rankIndex: 2));
    }

    [Theory] // D-012 (manual): fighters only; others fall back to THRUST rank 1 like DOS
    [InlineData(CharacterClass.Fighter, 11)]
    [InlineData(CharacterClass.Thief, 10)]
    [InlineData(CharacterClass.Ranger, 10)]
    [InlineData(CharacterClass.Wizard, 10)]
    public void LungeFor_FightersOnly(CharacterClass cls, int packed)
    {
        Assert.Equal(packed, CombatAction.LungeFor(cls));
        Assert.Equal(cls == CharacterClass.Fighter, CombatAction.CanLunge(cls));
    }

    [Fact]
    public void PartyToMonsterDamage_ActionPenaltyMakesThrustHarderThanSlash()
    {
        // Same roll/weapon/armor; THRUST(11) vs SLASH(31)
        var thrust = CombatMath.PartyToMonsterDamage(16, weaponDamageEff: 20, monsterArmor: 1, packedActionCode: 11);
        var slash = CombatMath.PartyToMonsterDamage(16, weaponDamageEff: 20, monsterArmor: 1, packedActionCode: 31);
        Assert.True(thrust > slash, $"thrust={thrust} should exceed slash={slash}");
    }

    [Fact]
    public void PartyToMonsterDamage_PinnedProduct()
    {
        // coef=4 (roll 16), weapon=20, armor=1, action=11 (THRUST)
        // product = 20*4 - 10 + 35 - (11*100)/67 = 80 - 10 + 35 - 16 = 89
        // damage = 89/10 = 8
        Assert.Equal(8, CombatMath.PartyToMonsterDamage(16, 20, 1, 11));
    }

    [Theory]
    [InlineData(3, 0, 0)]
    [InlineData(10, 1, 0)]
    [InlineData(11, 1, 1)]
    [InlineData(22, 2, 2)]
    [InlineData(31, 3, 1)]
    public void CombatAction_EncodeDecode(int packed, int swings, int rank)
    {
        var (s, r) = CombatAction.Decode(packed);
        Assert.Equal(swings, s);
        Assert.Equal(rank, r);
        if (packed != CombatAction.Parry)
            Assert.Equal(packed, CombatAction.Encode(swings, rank));
    }

    [Fact]
    public void CombatGear_FillsDosEquipSelectorFields()
    {
        var ch = new Character
        {
            WornShield = 4,   // points = 4
            WornArmor = 22,   // points = 2 (ROBES)
            WornWeapon = 48,  // club → points = 4
            WeaponDamageA = 9,
        };
        CombatGear.ApplyWornItemPoints(ch);
        Assert.Equal(4, ch.ShieldSoak);
        Assert.Equal(2, ch.ArmorSoak);
        Assert.Equal(9, ch.WeaponDamageA); // OV1 0x2fdb does not write @28.
        Assert.Equal(4, ch.WeaponDamageB); // OV1 0x2fdb writes weapon points to @240.
        Assert.Equal(4, ItemRules.CombatGate(48).Points);
    }

    [Fact]
    public void Prepare_CopiesEffectiveWeaponAndDefense()
    {
        var party = new Party();
        var hero = new Character
        {
            Name = "Hero",
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            Skills = new CharacterSkills(40, 40, 0, 0, 0, 0, 0, 0),
            WeaponDamageA = 5,
            WeaponDamageB = 3,
            ArmorSoak = 2,
            ShieldSoak = 6,
            HpCur = 20,
            HpMax = 20,
        };
        party.Members.Add(hero);

        var enc = new CombatEncounter(party);
        enc.Prepare();

        Assert.Equal(8, enc.Prep[0].WeaponDamageEff);
        Assert.Equal(40, enc.Prep[0].WeaponHitEff);
        Assert.Equal(2, enc.Prep[0].ArmorEff);
        Assert.Equal(22, enc.Prep[0].DefenseRating);
        Assert.Equal(22, hero.Word12); // DOS writes the rating into record @12
    }

    [OriginalDataFact]
    public void Prepare_MatchesDosCombatFieldsOfEveryFixtureCharacter()
    {
        // GUILD.pass4.dat holds @246/@248/@250/@12 as DOS's last combat prep wrote them (OV2 0x3263).
        var raw = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GUILD.pass4.dat"));
        var slots = GuildCodec.ReadBinary(raw, GameDataRepository.LoadPhantasie1().CreationTables);
        var party = new Party();
        var recordIndex = new List<int>();
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] is { } ch && ch.InParty)
            {
                party.Members.Add(ch);
                recordIndex.Add(i);
            }
        }
        Assert.Equal(6, party.Members.Count);

        var enc = new CombatEncounter(party);
        enc.Prepare();

        int U16(int rec, int off) => raw[rec * GuildCodec.RecordSize + off] | (raw[rec * GuildCodec.RecordSize + off + 1] << 8);
        for (var m = 0; m < party.Members.Count; m++)
        {
            var rec = recordIndex[m];
            Assert.Equal(U16(rec, 246), enc.Prep[m].ArmorEff);
            Assert.Equal(U16(rec, 248), enc.Prep[m].WeaponDamageEff);
            Assert.Equal(U16(rec, 250), enc.Prep[m].WeaponHitEff);
            Assert.Equal(U16(rec, 12), enc.Prep[m].DefenseRating);
        }
    }

    [Fact]
    public void Prepare_WakesSleepersAtFightStartOnly()
    {
        var party = new Party();
        var sleeper = new Character { Name = "S", Status = CharacterStatus.Sleep, HpCur = 5, HpMax = 5 };
        var dead = new Character { Name = "D", Status = CharacterStatus.Dead };
        party.Members.Add(sleeper);
        party.Members.Add(dead);

        var enc = new CombatEncounter(party);
        enc.Prepare(fightStart: false);
        Assert.Equal(CharacterStatus.Sleep, sleeper.Status);

        enc.Prepare(); // OV2 0x32fb: non-DEAD → OK
        Assert.Equal(CharacterStatus.Ok, sleeper.Status);
        Assert.Equal(CharacterStatus.Dead, dead.Status);
    }

    [OriginalDataFact]
    public void ResolveRound_PartyThrustKillsViper_DepositsRewardsSemantics()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var viper = data.GetMonster(6).ToCombatant(new SeededRandomSource(1));
        viper.HitPoints = 5;
        viper.Defense = 0; // guarantee hit chance stays positive
        viper.Armor = 0;

        var party = new Party();
        var hero = new Character
        {
            Name = "Fighter",
            Status = CharacterStatus.Ok,
            Stats = new CharacterStats(15, 10, 12, 12, 10, 10),
            Skills = new CharacterSkills(80, 20, 0, 0, 0, 0, 0, 0),
            HpCur = 40,
            HpMax = 40,
            WeaponDamageA = 30,
            ArmorSoak = 2,
            ShieldSoak = 4,
        };
        party.Members.Add(hero);

        var enc = new CombatEncounter(party) { RewardGold = 50 };
        enc.AddRank(viper);
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));

        // pass-1 readiness 0 (< 3·1 + 5), hit roll 0 (< chance), damage roll 16 (coef 4) — enough to kill 5 HP
        var rng = new ScriptedRandomSource(0, 0, 16, 99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.True(result.PartyWon);
        Assert.True(result.EncounterOver);
        Assert.Equal(732, result.RewardXp); // VIPER +0x1a, paid per kill (OV2 0x1656)
        Assert.Equal(50, result.RewardGold);
        Assert.Contains(result.Log, l => l.Contains("HITS", StringComparison.Ordinal));
        Assert.Contains(result.Log, l => l.Contains("DIES", StringComparison.Ordinal));

        var sack = new LootSack();
        sack.DepositRewards(result.RewardXp, result.RewardGold);
        Assert.Equal(732, sack.Experience);
        Assert.Equal(50, sack.Gold);
    }

    [Fact]
    public void ResolveRound_ParryAbsorbsShieldSoakOnce()
    {
        var party = new Party();
        var hero = new Character
        {
            Name = "Tank",
            Status = CharacterStatus.Ok,
            Stats = new CharacterStats(12, 8, 10, 14, 8, 8),
            Skills = new CharacterSkills(30, 40, 0, 0, 0, 0, 0, 0),
            HpCur = 50,
            HpMax = 50,
            ArmorSoak = 0,
            ShieldSoak = 5,
        };
        party.Members.Add(hero);

        var mon = new Monster
        {
            Name = "RAT",
            HitPoints = 20,
            DamageField = 15,
            AttackPower = 0x52, // exactly 1 swing
            Defense = 99, // party won't hit if they swung; they parry
            Armor = 0,
            CombatRating = 5,
        };

        var enc = new CombatEncounter(party);
        enc.AddRank(mon);
        enc.Prepare();
        enc.SetAction(0, CombatAction.Parry);

        // Monster: hit roll 0 (0*6 < metric), damage roll 16 → coef 4
        // damage = (4*15)/15 - 0 = 4; parry → 4-5 = -1 → floor 1
        var metric = CombatMath.MonsterMeleeMetric(mon.AttackPower, 1, enc.Prep[0].DefenseRating);
        Assert.True(CombatMath.MonsterMeleeHits(0, metric));

        var hpBefore = hero.HpCur;
        // PARRY never rolls readiness. Monster: readiness 0, special gate 0, target 0, hit 0, damage 16.
        var rng = new ScriptedRandomSource(0, 0, 0, 0, 16, 99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        Assert.False(result.EncounterOver);
        Assert.Equal(hpBefore - 1, hero.HpCur);
        Assert.Equal(CombatAction.Cleared, enc.Prep[0].PackedAction); // parry consumed
    }

    [Fact]
    public void SpellScaledDamage_ArmorResistedPath()
    {
        // roll 50, param 100 → scaled 50; /10 - armor 2 = 3
        Assert.Equal(3, CombatMath.SpellScaledDamage(50, 100, param2: 2, armorEff: 2));
        Assert.Equal(5, CombatMath.SpellScaledDamage(50, 100, param2: 1, armorEff: 2));
    }

    [OriginalDataFact]
    public void Viper_ToCombatant_CopiesDosRecordFields()
    {
        // DS:0x1a6c record 6 (s22): armor +0x14, damage +0x16, level +0x18, CV +0x25,
        // defense +0x2d, attack power +0x37 — identical in the DOSBox capture.
        var viper = GameDataRepository.LoadPhantasie1().GetMonster(6).ToCombatant(new SeededRandomSource(1));
        Assert.Equal(3, viper.Armor);
        Assert.Equal(14, viper.DamageField);
        Assert.Equal(3, viper.Level);
        Assert.Equal(12, viper.CombatRating);
        Assert.Equal(-10, viper.Defense);
        Assert.Equal(80, viper.AttackPower);
        Assert.Equal(732, viper.ExperienceValue);
        Assert.Equal(200, viper.Gold);
        Assert.InRange(viper.HitPoints!.Value, 10, 30); // 50 %…149 % of hp_base 20
    }

    [OriginalDataFact]
    public void BeginCombat_SetsEncounterLevelFromMonsterLevel()
    {
        // L = Σ group level +0x18 (OV2 0x3ac4); the Core stub builds one group. VIPER level 3.
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(3));
        session.Party.Members.Add(new Character
        {
            Name = "Hero", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        });
        var enc = session.BeginWildernessCombat(6);
        Assert.Equal(3, enc.MonsterLevel);
    }

    [Theory]
    [InlineData(0, 20, 10)]     // (50·20 + 50)/100
    [InlineData(99, 20, 30)]    // (149·20 + 50)/100 = 30.3
    [InlineData(50, 3, 3)]      // (100·3 + 50)/100
    [InlineData(0, 1, 1)]       // (50 + 50)/100
    [InlineData(99, 666, 992)]  // ZEUS: (149·666 + 50)/100 = 992.84
    public void MonsterHitPoints_IsOv2EncounterRoll(int roll, int hpBase, int expected) =>
        Assert.Equal(expected, CombatMath.MonsterHitPoints(roll, hpBase));

    // --- Round scheduler (OV2 0x00e4, combat-resolution.md §Turn order) ---

    [Theory]
    [InlineData(7, 1, 1, true)]    // 3·1 + 5 = 8 > 7
    [InlineData(8, 1, 1, false)]
    [InlineData(47, 1, 3, true)]   // 3 + 45 = 48
    [InlineData(48, 1, 3, false)]
    [InlineData(99, 1, 5, true)]   // pass 5: 3 + 125 — always
    [InlineData(34, 10, 1, true)]  // 30 + 5 = 35
    public void PartyActsInPass_Is3LevelPlus5PassSquared(int roll, int level, int pass, bool acts) =>
        Assert.Equal(acts, CombatMath.PartyActsInPass(roll, level, pass));

    [Theory]
    [InlineData(7, 1, 0, true)]    // 49 ≤ 50
    [InlineData(8, 1, 0, false)]   // 56 > 50
    [InlineData(11, 1, 1, true)]   // 77 ≤ 50 + 30·1
    [InlineData(12, 1, 1, false)]  // 84 > 80
    [InlineData(64, 3, 0, true)]   // 448 ≤ 450
    [InlineData(65, 3, 0, false)]
    [InlineData(99, 5, 0, true)]   // 693 ≤ 1250 — always
    public void MonsterActsInPass_Is7RollVs50PassSquaredPlus30Level(int roll, int pass, int level, bool acts) =>
        Assert.Equal(acts, CombatMath.MonsterActsInPass(roll, pass, level));

    [Theory]
    [InlineData(0, 0, 3, 0)]       // k-th of n monsters aims near slot k·6/n
    [InlineData(0, 1, 3, 2)]
    [InlineData(0, 2, 3, 4)]
    [InlineData(99, 2, 3, 5)]      // (9 + 20)·6/3 = 58 → 5
    [InlineData(99, 0, 1, 5)]      // (9)·6/1 = 54 → 5
    public void MonsterTargetSlot_SpreadsAttacksAcrossParty(int roll, int slot, int n, int expected)
    {
        var party = Enumerable.Range(0, 6).Select(_ => new Character { Status = CharacterStatus.Ok, HpCur = 5 }).ToList();
        Assert.Equal(expected, CombatMath.MonsterTargetSlot(roll, slot, n, party));
    }

    [Fact]
    public void MonsterTargetSlot_SkipsDeadAndWrapsButHitsSleepers()
    {
        var party = new List<Character>
        {
            new() { Status = CharacterStatus.Sleep, HpCur = 5 },
            new() { Status = CharacterStatus.Ok, HpCur = 5 },
            new() { Status = CharacterStatus.Dead },
        };
        Assert.Equal(0, CombatMath.MonsterTargetSlot(99, 1, 2, party)); // (9+10)·3/2/10 = 2 → dead → wraps to 0 (asleep)
        party[0].Status = CharacterStatus.Dead;
        Assert.Equal(1, CombatMath.MonsterTargetSlot(0, 0, 1, party));
        party[1].Status = CharacterStatus.Dead;
        Assert.Equal(-1, CombatMath.MonsterTargetSlot(0, 0, 1, party));
    }

    private static Character Fighter(string name, int level = 1) => new()
    {
        Name = name,
        Level = level,
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(15, 10, 12, 12, 10, 10),
        Skills = new CharacterSkills(80, 20, 0, 0, 0, 0, 0, 0),
        HpCur = 40,
        HpMax = 40,
        WeaponDamageA = 5,
    };

    private static Monster Rat(string name = "RAT") => new()
    {
        Name = name,
        HitPoints = 50,
        DamageField = 15,
        AttackPower = 0x52, // 1 swing
        Defense = 99,       // party always misses
        CombatRating = 5,
    };

    [Fact]
    public void ResolveRound_SlowMemberActsAfterMonstersOfPass1()
    {
        var party = new Party();
        party.Members.Add(Fighter("SLOW"));
        var enc = new CombatEncounter(party);
        enc.AddRank(Rat());
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));

        // pass 1: party readiness 50 (≥ 8, waits); rat readiness 0, gate 0, target 0, hit 99 (miss), …
        // pass 3: party readiness 0 → swings (hit roll 99 misses).
        var rng = new ScriptedRandomSource(50, 0, 0, 0, 99, 0, 99);
        var result = new CombatResolver().ResolveRound(enc, rng);

        var ratLine = result.Log.ToList().FindIndex(l => l.StartsWith("RAT ", StringComparison.Ordinal));
        var slowLine = result.Log.ToList().FindIndex(l => l.StartsWith("SLOW ", StringComparison.Ordinal));
        Assert.True(ratLine >= 0 && slowLine > ratLine, string.Join(" | ", result.Log));
        Assert.Equal(CombatAction.Cleared, enc.Prep[0].PackedAction);
    }

    [OriginalDataFact]
    public void ResolveRound_EveryoneActsExactlyOncePerRound()
    {
        var party = new Party();
        party.Members.Add(Fighter("A"));
        party.Members.Add(Fighter("B"));
        var enc = new CombatEncounter(party);
        enc.AddRank(Rat("R1"), Rat("R2"));
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));
        enc.SetAction(1, CombatAction.Attack(0));

        // Every roll 0: all ready in pass 1, every swing hits for coef 0 → the floor of 1 damage.
        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));

        int Swings(string who) => result.Log.Count(l =>
            l.StartsWith(who + " HITS", StringComparison.Ordinal) || l.StartsWith(who + " MISSES", StringComparison.Ordinal));
        Assert.Equal(1, Swings("A"));  // THRUST
        Assert.Equal(2, Swings("B"));  // ATTACK
        Assert.Equal(1, Swings("R1"));
        Assert.Equal(1, Swings("R2"));
    }

    [OriginalDataFact]
    public void ResolveRound_SleepingMemberWakesOnRollAtMost16AndDoesNotAct()
    {
        var party = new Party();
        var sleeper = Fighter("NAPPER");
        party.Members.Add(sleeper);
        var enc = new CombatEncounter(party);
        enc.AddRank(Rat());
        enc.Prepare();
        sleeper.Status = CharacterStatus.Sleep;
        enc.SetAction(0, CombatAction.Cleared);

        // pass 1: wake roll 16 → awake; rat readiness 99 (693 > 50, waits). pass 3: NAPPER has no action;
        // rat readiness 99 (693 > 450). pass 5: rat acts (gate, target, hit 99 → miss).
        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(16, 99));

        Assert.Equal(CharacterStatus.Ok, sleeper.Status);
        Assert.Contains("NAPPER AWAKENS", result.Log);
        Assert.DoesNotContain(result.Log, l => l.StartsWith("NAPPER HITS") || l.StartsWith("NAPPER MISSES"));
    }

    [Fact]
    public void ResolveRound_ThirdRankNeverMelees()
    {
        var party = new Party();
        party.Members.Add(Fighter("A"));
        var enc = new CombatEncounter(party);
        enc.AddRank(Rat("R1"));
        enc.AddRank(Rat("R2"));
        enc.AddRank(Rat("R3"));
        enc.Prepare();
        enc.SetAction(0, CombatAction.Parry);

        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));

        Assert.Contains(result.Log, l => l.StartsWith("R1 ", StringComparison.Ordinal));
        Assert.Contains(result.Log, l => l.StartsWith("R2 ", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Log, l => l.StartsWith("R3 ", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveRound_ReRanksByLivingCountTiesToLaterGroup()
    {
        var party = new Party();
        party.Members.Add(Fighter("A"));
        var enc = new CombatEncounter(party);
        var g0 = enc.AddRank(Rat("A1"));
        var g1 = enc.AddRank(Rat("B1"));
        var g2 = enc.AddRank(Rat("C1"), Rat("C2"));
        enc.Prepare();
        enc.SetAction(0, CombatAction.Parry);

        new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));

        // C (2) first; A and B tie at 1 → the later group (B) goes first.
        Assert.Equal(new[] { g2, g1, g0 }, enc.Ranks);
    }

    [OriginalDataFact]
    public void ResolveRound_GroupWipedThisRoundDropsToTheBackAtRoundEnd()
    {
        var party = new Party();
        party.Members.Add(Fighter("A"));
        var enc = new CombatEncounter(party);
        var front = enc.AddRank(Rat("F1"));
        var back = enc.AddRank(Rat("B1"));
        front.Members[0].HitPoints = 1;
        front.Members[0].Defense = 0;
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));

        // A ready (0), hit (0), damage floor 1 kills F1; B1 then acts.
        var result = new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0));

        Assert.Contains("F1 DIES", result.Log);
        Assert.Equal(new[] { back, front }, enc.Ranks); // fresh counts (0x1694 re-compacts), no one-round lag
    }

    // --- Action chooser (OV2 0x6bc5 + D-011/D-012) ---

    private static (Character Ch, CombatantPrep Prep) Chooser(CharacterClass cls, int attack = 160, int parry = 40)
    {
        var ch = Fighter("X");
        ch.Class = cls;
        ch.Skills = new CharacterSkills(attack, parry, 0, 0, 0, 0, 0, 0);
        return (ch, new CombatantPrep { WeaponHitEff = attack });
    }

    [Theory]
    [InlineData(40, 40, CombatAction.Parry)]    // roll ≤ parry → PARRY
    [InlineData(41, 40, CombatAction.Cleared)]  // else no action this round
    [InlineData(0, 0, CombatAction.Parry)]
    public void Choose_ParryRollsAgainstParrySkill(int roll, int parry, int expected)
    {
        var (ch, prep) = Chooser(CharacterClass.Fighter, parry: parry);
        Assert.Equal(expected, CombatOrders.Choose(ch, prep, CombatChoice.Parry, new ScriptedRandomSource(roll)));
    }

    [Fact]
    public void Choose_OnlyThievesPickARank()
    {
        var (fighter, fp) = Chooser(CharacterClass.Fighter);
        var (thief, tp) = Chooser(CharacterClass.Thief);
        var rng = new ScriptedRandomSource(0);
        Assert.Equal(CombatAction.Attack(0), CombatOrders.Choose(fighter, fp, CombatChoice.Attack, rng, rankIndex: 2));
        Assert.Equal(CombatAction.Attack(2), CombatOrders.Choose(thief, tp, CombatChoice.Attack, rng, rankIndex: 2));
        Assert.Equal(CombatAction.Slash(3, 1), CombatOrders.Choose(thief, tp, CombatChoice.Slash, rng, rankIndex: 1));
    }

    [Fact]
    public void Choose_SlashAndLungeFollowManualDecisions()
    {
        var rng = new ScriptedRandomSource(0);
        var (weak, wp) = Chooser(CharacterClass.Priest, attack: 20);
        Assert.Equal(CombatAction.Slash(3, 0), CombatOrders.Choose(weak, wp, CombatChoice.Slash, rng)); // D-011: no gate
        Assert.Equal(CombatAction.Attack(0), CombatOrders.Choose(weak, wp, CombatChoice.Attack, rng));
        var (strong, sp) = Chooser(CharacterClass.Fighter, attack: 225);
        Assert.Equal(CombatAction.Slash(4, 0), CombatOrders.Choose(strong, sp, CombatChoice.Slash, rng));
        Assert.Equal(CombatAction.Lunge(), CombatOrders.Choose(strong, sp, CombatChoice.Lunge, rng));
        var (thief, tp) = Chooser(CharacterClass.Thief);
        Assert.Equal(CombatAction.Thrust(0), CombatOrders.Choose(thief, tp, CombatChoice.Lunge, rng));    // D-012
    }

    [Fact]
    public void Choose_UnusableSpellFallsBackToParryRoll()
    {
        var (ch, prep) = Chooser(CharacterClass.Fighter, parry: 50);
        Assert.Equal(CombatAction.Parry,
            CombatOrders.Choose(ch, prep, CombatChoice.Cast, new ScriptedRandomSource(10), spellIndex: SpellId.FireflashI));
        Assert.Equal(CombatAction.Cleared,
            CombatOrders.Choose(ch, prep, CombatChoice.Cast, new ScriptedRandomSource(90), spellIndex: SpellId.FireflashI));
    }

    // --- Death CON loss (OV2 0x12cf, D-009) ---

    [Theory]
    [InlineData(10, 8, 2)]   // Luck 8–10 lose 2
    [InlineData(10, 10, 2)]
    [InlineData(10, 11, 1)]  // Luck 11–13 lose 1
    [InlineData(10, 13, 1)]
    [InlineData(1, 8, 1)]    // CON 1: only the first point
    [InlineData(0, 8, 0)]    // CON 0: nothing left, no floor below 0
    public void DeathConLoss_MatchesDosRule(int con, int luck, int expected) =>
        Assert.Equal(expected, CombatResolver.DeathConLoss(new CharacterStats(10, 10, 10, con, 10, luck)));

    [Fact]
    public void Survivors_CountsSleepersNotDeadNorElemental()
    {
        var party = new Party();
        party.Members.Add(new Character { Name = "A", Status = CharacterStatus.Ok });
        party.Members.Add(new Character { Name = "S", Status = CharacterStatus.Sleep });
        party.Members.Add(new Character { Name = "D", Status = CharacterStatus.Dead });
        var elemental = new Character { Name = "E", Status = CharacterStatus.Ok };
        party.Elemental = elemental;
        party.Members.Add(elemental);
        Assert.Equal(2, CombatEncounter.Survivors(party)); // 0x43ae tests @24 != 0; slot 7 left at 0x3f2e
    }

    [Fact]
    public void LootSack_XpPoolCapsAtFfffTens()
    {
        var sack = new LootSack { Experience = CombatEncounter.XpPoolCap - 20 };
        sack.DepositRewards(100, 0);
        Assert.Equal(655_350, sack.Experience);
    }

    [OriginalDataFact]
    public void Session_EndOfCombat_DepositsEncounterTotals_D053()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Beggar", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        session.Combat!.RewardXp = 308;
        var sleeper = new Character { Name = "Z", Status = CharacterStatus.Sleep, HpCur = 5, HpMax = 5 };
        session.Party.Members.Add(sleeper); // DOS would pay 308 / 20 = 15 → 150 each, pool 300
        session.ChoosePartyOption(PartyOption.BegMercy);

        // Manual (D-053): the whole total is shown and pooled; the split waits for town entry.
        Assert.Null(session.Combat);
        Assert.Equal(308, session.Sack.Experience);
        Assert.Contains("308 EXPERIENCE POINTS AND 0 GOLD", session.Log);
        Assert.DoesNotContain(session.Log, l => l.StartsWith("EACH SURVIVOR", StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void Session_DayWildernessFightFlashesADayEncounter_D058()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Seer", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        var mark = session.LogSequence;
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);

        var lines = session.LogSince(mark).ToList();
        Assert.Equal(GameSession.DayEncounterBanner, lines[0]); // before the fight's own lines (user s53: day / night)
        Assert.Equal("A DAY ENCOUNTER", GameSession.DayEncounterBanner);
    }
}
