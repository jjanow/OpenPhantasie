using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>OV2 encounter builder <c>0x34aa</c> (<c>combat-resolution.md</c> §Monster record & encounter build).</summary>
public class EncounterBuilderTests
{
    /// <summary>Hands out the scripted rolls in order and fails if the builder draws more than were scripted.</summary>
    private sealed class StrictRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public StrictRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Remaining => _values.Count;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.Equal((0, 100), (minInclusive, maxExclusive));
            Assert.NotEmpty(_values);
            return _values.Dequeue();
        }
    }

    private static Monster Template(
        int id, int width = 30, int factor = 6, int chance = 0, int companionA = 0, int companionB = 0,
        int level = 1, long gold = 0, long xp = 0, int hpBase = 20, params int[] treasure) => new()
    {
        Id = id,
        Name = $"M{id}",
        SpriteWidth = width,
        GroupSizeFactor = factor,
        NextGroupChance = chance,
        Companions = new[] { companionA, companionB },
        Level = level,
        Gold = gold,
        ExperienceValue = xp,
        HpBase = hpBase,
        TreasureItems = treasure,
    };

    private static CombatEncounter Build(Dictionary<int, Monster> table, int first, IRandomSource rng, bool blackKnightSpot = false) =>
        EncounterBuilder.Build(new Party(), first, id => CopyOf(table[id]), rng, blackKnightSpot);

    private static Monster CopyOf(Monster t) => Template(
        t.Id, t.SpriteWidth, t.GroupSizeFactor, t.NextGroupChance, t.Companions[0], t.Companions[1],
        t.Level, t.Gold, t.ExperienceValue, t.HpBase, t.TreasureItems.ToArray());

    [Theory]
    [InlineData(99, 6)] // 99·6/102 = 5 → 6
    [InlineData(17, 2)] // 102/102 = 1 → 2
    [InlineData(16, 1)] // 96/102 = 0 → 1
    [InlineData(0, 1)]
    public void CountRoll_IsClampedFactorScale(int roll, int expectedCount)
    {
        var table = new Dictionary<int, Monster> { [0] = Template(0, factor: 6, chance: 90) };
        var rolls = new List<int> { roll };
        rolls.AddRange(Enumerable.Repeat(50, expectedCount)); // HP rolls
        rolls.Add(0);  // companion
        rolls.Add(99); // next group: 99 ≥ 90 → stop
        var rng = new StrictRandomSource(rolls.ToArray());

        var enc = Build(table, 0, rng);

        Assert.Single(enc.Ranks);
        Assert.Equal(expectedCount, enc.Ranks[0].Members.Count);
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void CountNeverExceedsSix()
    {
        // ORANGE-DRAGON-sized factors don't exist, but the clamp is 0..5 (0x39f8).
        var table = new Dictionary<int, Monster> { [0] = Template(0, factor: 127) };
        var rolls = new List<int> { 99 };
        rolls.AddRange(Enumerable.Repeat(0, 6));
        rolls.AddRange(new[] { 0, 99 });
        var enc = Build(table, 0, new StrictRandomSource(rolls.ToArray()));
        Assert.Equal(EncounterBuilder.MaxGroupSize, enc.Ranks[0].Members.Count);
    }

    [Fact]
    public void HpRolledPerMonster()
    {
        var table = new Dictionary<int, Monster> { [0] = Template(0, factor: 102, hpBase: 20) };
        // count roll 1 → 1·102/102 = 1 → 2 monsters; HP rolls 0 and 99.
        var enc = Build(table, 0, new StrictRandomSource(1, 0, 99, 0, 99));
        Assert.Equal(new int?[] { 10, 30 }, enc.Ranks[0].Members.Select(m => m.HitPoints).ToArray());
    }

    [Theory]
    [InlineData(49, 2)] // rand < 50 → +0x28 (Companions[1])
    [InlineData(50, 1)] // else +0x27 (Companions[0])
    public void CompanionPick_SplitsAtFifty(int roll, int expectedId)
    {
        var table = new Dictionary<int, Monster>
        {
            [0] = Template(0, factor: 0, chance: 100, companionA: 1, companionB: 2),
            [1] = Template(1, factor: 0),
            [2] = Template(2, factor: 0),
        };
        // g0: count, HP, companion, next (0 < 100). g1: count, HP, companion, next (99 ≥ 0 → stop).
        var enc = Build(table, 0, new StrictRandomSource(0, 50, roll, 0, 0, 50, 0, 99));
        Assert.Equal(2, enc.Ranks.Count);
        Assert.Equal(expectedId, enc.Ranks[1].Members[0].Id);
    }

    [Fact]
    public void AtMostThreeGroups_LastGroupStillDrawsNextRoll()
    {
        var table = new Dictionary<int, Monster> { [0] = Template(0, width: 10, factor: 0, chance: 100) };
        // Three groups × (count, HP, companion, next) — the third group's next roll is drawn too (0x3b22).
        var rng = new StrictRandomSource(Enumerable.Repeat(0, 12).ToArray());
        var enc = Build(table, 0, rng);
        Assert.Equal(EncounterBuilder.MaxGroups, enc.Ranks.Count);
        Assert.Equal(0, rng.Remaining);
    }

    [Fact]
    public void WideSecondGroup_IsDroppedWithoutRolls()
    {
        // Two 80-wide sprites: 83 + 83 = 166 ≥ 135 → group 1 dropped even though its next-group roll passed.
        var table = new Dictionary<int, Monster>
        {
            [0] = Template(0, width: 80, factor: 0, chance: 100, companionA: 1, companionB: 1, level: 4, gold: 9),
            [1] = Template(1, width: 80, factor: 0, level: 7, gold: 1000),
        };
        var rng = new StrictRandomSource(0, 50, 0, 0);
        var enc = Build(table, 0, rng);
        Assert.Single(enc.Ranks);
        Assert.Equal(4, enc.MonsterLevel);
        Assert.Equal(9, enc.RewardGold);
        Assert.Equal(0, rng.Remaining);
    }

    [Theory]
    [InlineData(62, 2)] // 69 + 65 = 134 < 135 → room
    [InlineData(63, 1)] // 69 + 66 = 135 → no room
    public void WidthLimit_IsStrictlyBelow135(int secondWidth, int expectedGroups)
    {
        var table = new Dictionary<int, Monster>
        {
            [0] = Template(0, width: 66, factor: 0, chance: 100, companionA: 1, companionB: 1),
            [1] = Template(1, width: secondWidth, factor: 0),
        };
        var rolls = expectedGroups == 2 ? new[] { 0, 50, 0, 0, 0, 50, 0, 99 } : new[] { 0, 50, 0, 0 };
        var enc = Build(table, 0, new StrictRandomSource(rolls));
        Assert.Equal(expectedGroups, enc.Ranks.Count);
    }

    [Fact]
    public void LevelPerGroup_GoldPerMonster_TreasureFromGroupZero_XpStartsAtZero()
    {
        var table = new Dictionary<int, Monster>
        {
            [0] = Template(0, factor: 102, chance: 100, companionA: 1, companionB: 1, level: 3, gold: 20, xp: 77, treasure: new[] { 58, 59 }),
            [1] = Template(1, factor: 0, level: 5, gold: 7, xp: 500, treasure: new[] { 18, 67 }),
        };
        // g0: count 1 → 2 monsters, 2 HP, companion, next 0. g1: count, HP, companion, next 99.
        var enc = Build(table, 0, new StrictRandomSource(1, 50, 50, 0, 0, 0, 50, 0, 99));

        Assert.Equal(new[] { 2, 1 }, enc.Ranks.Select(r => r.Members.Count).ToArray());
        Assert.Equal(8, enc.MonsterLevel);   // 3 + 5, once per group — not 3 + 3 + 5
        Assert.Equal(47, enc.RewardGold);    // 20 + 20 + 7
        Assert.Equal(new[] { 58, 59 }, enc.TreasureItemIds);
        Assert.Equal(0, enc.RewardXp);
    }

    [Fact]
    public void BlackKnightSpot_ForcesFactorFour()
    {
        var table = new Dictionary<int, Monster> { [76] = Template(76, factor: 3) };
        // roll 99: factor 3 → 2 → 3 monsters; factor 4 → 3 → 4 monsters.
        Assert.Equal(3, Build(table, 76, new StrictRandomSource(99, 0, 0, 0, 0, 99)).Ranks[0].Members.Count);
        Assert.Equal(4, Build(table, 76, new StrictRandomSource(99, 0, 0, 0, 0, 0, 99), blackKnightSpot: true)
            .Ranks[0].Members.Count);
    }

    [Fact]
    public void KillPaysMonsterXp()
    {
        var party = new Party();
        party.Members.Add(new Character
        {
            Name = "Hero", Class = CharacterClass.Fighter, Status = CharacterStatus.Ok, Level = 1,
            HpCur = 40, HpMax = 40, WeaponDamageA = 30,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            Skills = new CharacterSkills(90, 0, 0, 0, 0, 0, 0, 0),
        });
        var enc = new CombatEncounter(party);
        var a = Template(0, xp: 77, hpBase: 1);
        a.HitPoints = 1;
        enc.AddRank(a);
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));

        // ready 0, hit 0, damage 16 → kills it. (The 0x8c1 group-XP bonus is DISSOLVE's: CombatSpellsTests.)
        var result = new CombatResolver().ResolveRound(enc, new StrictRandomSource(0, 0, 16));
        Assert.True(result.PartyWon);
        Assert.Equal(77, result.RewardXp);
    }

    [Fact]
    public void ScaleSpell_RollsOncePerGroup()
    {
        // OV2 0x1382: CONFUSION/WEAKNESS/BINDING roll per group and scale the shared group record.
        var party = new Party();
        var mage = new Character
        {
            Name = "Mage", Class = CharacterClass.Wizard, Status = CharacterStatus.Ok,
            HpCur = 20, HpMax = 20, MagicCur = 10, MagicMax = 10, MpCeiling = 10,
            SpellsKnown = new int[SpellId.Count],
        };
        mage.SpellsKnown[SpellId.WeaknessI] = 1;
        party.Members.Add(mage);
        var enc = new CombatEncounter(party);
        Monster Orc() => new() { Name = "ORC", HitPoints = 10, CombatRating = 1, AttackPower = 100 };
        var front = new[] { Orc(), Orc(), Orc() };
        var back = new[] { Orc(), Orc() };
        enc.AddRank(front);
        enc.AddRank(back);
        enc.Prepare();

        // chance (10 − 1)·5 + 50 = 95: rank 0 hit (0), rank 1 miss (99) — two rolls for five monsters.
        var rng = new StrictRandomSource(0, 99);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.WeaknessI), rng, new List<string>());

        Assert.Equal(0, rng.Remaining);
        Assert.All(front, m => Assert.True(m.AttackPower < 100));
        Assert.Single(front.Select(m => m.AttackPower).Distinct());
        Assert.All(back, m => Assert.Equal(100, m.AttackPower));
    }

    [Fact]
    public void ScaleSpell_WipedGroupStillUsesARoll()
    {
        // OV2 0x1382 loops every built group (< [0x6492]) and draws the resist roll before any living test (s33).
        var party = new Party();
        var mage = new Character
        {
            Name = "Mage", Class = CharacterClass.Wizard, Status = CharacterStatus.Ok,
            HpCur = 20, HpMax = 20, MagicCur = 10, MagicMax = 10, MpCeiling = 10,
            SpellsKnown = new int[SpellId.Count],
        };
        mage.SpellsKnown[SpellId.WeaknessI] = 1;
        party.Members.Add(mage);
        var enc = new CombatEncounter(party);
        Monster Orc(int hp) => new() { Name = "ORC", HitPoints = hp, CombatRating = 1, AttackPower = 100 };
        var dead = new[] { Orc(0), Orc(0) };
        var living = new[] { Orc(10) };
        enc.AddRank(dead);
        enc.AddRank(living);
        enc.Prepare();

        var rng = new StrictRandomSource(0, 0); // one roll for the wiped rank, one for the living rank
        var log = new List<string>();
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.WeaknessI), rng, log);

        Assert.Equal(0, rng.Remaining);
        Assert.True(living[0].AttackPower < 100);
        Assert.Single(log); // [D✓] s65: only "… CASTS A SPELL" — DOS prints no WEAKNESS line
    }

    [OriginalDataFact]
    public void Session_BeginCombat_UsesRealRecords()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        session.Party.Members.Add(new Character
        {
            Name = "Hero", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        });
        var enc = session.BeginWildernessCombat(0); // SKELETON: f 6, chance 90, companion SKELETON, width 34

        Assert.InRange(enc.Ranks.Count, 1, 3);
        Assert.All(enc.Ranks, r => Assert.InRange(r.Members.Count, 1, 6));
        Assert.All(enc.AllMonsters, m => Assert.Equal("SKELETON", m.Name));
        Assert.Equal(enc.Ranks.Count, enc.MonsterLevel); // level 1 per group
        Assert.Equal(20L * enc.AllMonsters.Count(), enc.RewardGold);
        Assert.Equal(new[] { 58, 59 }, enc.TreasureItemIds);
    }

    [OriginalDataFact]
    public void Snapshot_RoundTripsGroupsAfterReRank()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Grp", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        var enc = session.BeginWildernessCombat(0);

        Monster Rec(int id, int hp)
        {
            var m = data.GetMonster(id).ToRecord();
            m.HitPoints = hp;
            return m;
        }

        enc.Ranks.Clear();
        enc.AddRank(Rec(0, 3), Rec(0, 0));          // 1 living
        enc.AddRank(Rec(6, 9), Rec(6, 8), Rec(6, 7)); // 3 living
        enc.AddRank(Rec(3, 5), Rec(3, 4));          // 2 living
        enc.MonsterLevel = 11;
        enc.RewardXp = 77;
        enc.ReRank();
        Assert.Equal(new[] { 1, 2, 0 }, enc.Ranks.Select(r => r.RankIndex).ToArray());

        var json = SessionCodec.WriteJson(session.CaptureSnapshot());
        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(json));

        var c = loaded.Combat!;
        Assert.Equal(new[] { 1, 2, 0 }, c.Ranks.Select(r => r.RankIndex).ToArray());
        Assert.Equal(new[] { 3, 2, 2 }, c.Ranks.Select(r => r.Members.Count).ToArray());
        Assert.Equal(new int?[] { 9, 8, 7 }, c.Ranks[0].Members.Select(m => m.HitPoints).ToArray());
        Assert.Equal(11, c.MonsterLevel);
        Assert.Equal(77, c.RewardXp);
        Assert.Equal(enc.RewardGold, c.RewardGold);
        Assert.Equal(enc.TreasureItemIds, c.TreasureItemIds);
    }
}
