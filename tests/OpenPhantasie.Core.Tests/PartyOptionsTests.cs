using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Per-round party options, OV2 <c>0x3b70</c> (<c>combat-resolution.md</c> §Party options).</summary>
public partial class PartyOptionsTests
{
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

    private static Character Member(int level) => new()
    {
        Name = $"L{level}",
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        Skills = new CharacterSkills(80, 0, 0, 0, 0, 0, 0, 0),
        Level = level,
        HpCur = 20,
        HpMax = 20,
    };

    private static Monster Mon(long xp = 100, int hp = 10) => new() { Name = "ORC", HitPoints = hp, ExperienceValue = xp };

    /// <summary>One group of <paramref name="count"/> monsters worth <paramref name="xp"/> each; L = 2, gold 500.</summary>
    private static CombatEncounter Setup(int count = 3, long xp = 100, int reaction = 0, params int[] levels)
    {
        var party = new Party();
        foreach (var l in levels.Length == 0 ? new[] { 1 } : levels)
            party.Members.Add(Member(l));
        var enc = new CombatEncounter(party) { MonsterLevel = 2, RewardGold = 500, ReactionSum = reaction };
        enc.AddRank(Enumerable.Range(0, count).Select(_ => Mon(xp)).ToArray());
        enc.Prepare();
        return enc;
    }

    // --- Morale check (0x3b98) ----------------------------------------------------------------

    [Fact]
    public void Morale_NoKills_Stands()
    {
        var enc = Setup();
        var rng = new StrictRandomSource(99, 0);
        Assert.Equal(MoraleOutcome.Stand, PartyOptions.MoraleCheck(enc, rng, terrainClass: 0));
        Assert.Equal(0, rng.Remaining);
    }

    [Theory]
    // 6 kills of 100 XP vs 3 living: 600·(r1 + 100)/100 ≥ (r2 + 200)·3 ?
    [InlineData(0, 0, true)]   // 600 ≥ 600
    [InlineData(0, 1, false)]  // 600 < 603
    [InlineData(1, 2, true)]   // 606 ≥ 606
    public void Morale_ComparesKillsAgainstLiving(int r1, int r2, bool tryToRun)
    {
        var enc = Setup();
        enc.RewardXp = 600;
        var rolls = new List<int> { r1, r2 };
        if (tryToRun) rolls.Add(99); // run roll 99 ≥ 70: they don't get away
        var rng = new StrictRandomSource(rolls.ToArray());
        var outcome = PartyOptions.MoraleCheck(enc, rng, terrainClass: 0);
        Assert.Equal(tryToRun ? MoraleOutcome.RunFailed : MoraleOutcome.Stand, outcome);
        Assert.Equal(0, rng.Remaining);
    }

    [Theory]
    [InlineData(0, 69, MoraleOutcome.Ran)]       // 80 − |10·(1−0)| = 70
    [InlineData(0, 70, MoraleOutcome.RunFailed)]
    [InlineData(1, 79, MoraleOutcome.Ran)]       // 80 − 0
    [InlineData(3, 59, MoraleOutcome.Ran)]       // 80 − |−20| = 60: abs, so rough terrain makes it harder
    [InlineData(3, 60, MoraleOutcome.RunFailed)]
    public void Morale_RunRoll_UsesAbsTerrainPenalty(int terrain, int runRoll, MoraleOutcome expected)
    {
        var enc = Setup();
        enc.RewardXp = 10_000;
        var rng = new StrictRandomSource(0, 0, runRoll);
        Assert.Equal(expected, PartyOptions.MoraleCheck(enc, rng, terrain));
    }

    [Fact]
    public void Morale_FailedRun_TriplesThreatenAndMonstersLoseTheRound()
    {
        var enc = Setup(count: 1);
        enc.RewardXp = 10_000;
        Assert.Equal(MoraleOutcome.RunFailed, PartyOptions.MoraleCheck(enc, new StrictRandomSource(0, 0, 99), 0));
        Assert.Equal(PartyOptions.ThreatenFactorAfterFailedRun, enc.ThreatenFactor);
        Assert.True(enc.MonstersLoseRound);

        // Party does nothing; the monster may not act either, and the flag is used up.
        foreach (var p in enc.Prep)
            p.PackedAction = CombatAction.Cleared;
        var hp = enc.Party.Members[0].HpCur;
        var result = new CombatResolver().ResolveRound(enc, new StrictRandomSource());
        Assert.False(result.EncounterOver);
        Assert.Equal(hp, enc.Party.Members[0].HpCur);
        Assert.False(enc.MonstersLoseRound);
    }

    [Fact]
    public void Morale_Ran_PaysFullGoldAndKillXp()
    {
        var enc = Setup();
        enc.RewardXp = 10_000;
        Assert.Equal(MoraleOutcome.Ran, PartyOptions.MoraleCheck(enc, new StrictRandomSource(0, 0, 0), 0));
        var end = PartyOptions.MonstersRan(enc);
        Assert.True(end.EncounterOver);
        Assert.Equal(10_000, end.RewardXp);
        Assert.Equal(500, end.RewardGold);
    }

    // --- Options -----------------------------------------------------------------------------

    [Fact]
    public void Sack_ForfeitToBeggars_ClearsGoldAndEveryOtherItemOfTheFirst50()
    {
        var sack = new LootSack();
        sack.DepositRewards(70, 900, Enumerable.Range(1, 55));
        sack.ForfeitToBeggars();
        Assert.Equal(0, sack.Gold);
        Assert.Equal(70, sack.Experience);
        // slots 0, 2, …, 48 emptied in place (items 1, 3, …, 49); 2, 4, …, 50 and 51..55 kept ([D✓] s65: holes stay).
        var expected = Enumerable.Range(1, 55).Select(i => i <= 49 && i % 2 == 1 ? 0 : i);
        Assert.Equal(expected, sack.ItemIds);
        Assert.Equal(30, sack.ItemCount);
    }

    [Fact]
    public void Threaten_UsesTheTownExitAverage_NotTheLiveOne()
    {
        // s41: [0x63ec] is written only at town Exit. Live avg 4 would give 0 + 12 − 6 = 6; the stale avg 1 gives −3.
        var enc = Setup(levels: new[] { 4, 5 });
        enc.RewardXp = 0;
        var r = PartyOptions.Choose(enc, PartyOption.Threaten, new StrictRandomSource(0), true, 0, averagePartyLevel: 1);
        Assert.Equal(PartyOptionOutcome.GiveOrders, r.Outcome);
    }

    [Fact]
    public void Threaten_AfterFailedRun_UsesFactor45()
    {
        var enc = Setup(levels: new[] { 1 });
        enc.RewardXp = 100;
        enc.ThreatenFactor = PartyOptions.ThreatenFactorAfterFailedRun;
        // 45·100/100 + 3 − 6 = 42
        var r = PartyOptions.Choose(enc, PartyOption.Threaten, new StrictRandomSource(42), false, 0);
        Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
        Assert.False(enc.SurrenderTreasure); // wilderness
    }

    [Theory]
    [InlineData(0, 9, true)]   // rand < Σ + 10
    [InlineData(0, 10, false)]
    [InlineData(-10, 0, false)]
    public void Greet_RollsAgainstReactionSum(int reaction, int roll, bool greeted)
    {
        var enc = Setup(reaction: reaction);
        enc.RewardXp = 50;
        var r = PartyOptions.Choose(enc, PartyOption.Greetings, new StrictRandomSource(roll), false, 0);
        Assert.Equal(greeted ? PartyOptionOutcome.CombatOver : PartyOptionOutcome.GiveOrders, r.Outcome);
        if (greeted)
        {
            Assert.Equal(0, r.End!.RewardGold);
            Assert.Equal(50, r.End.RewardXp);
        }
    }

    [Fact]
    public void Greet_FailsWithoutRollOnceAMonsterWasDamaged()
    {
        var enc = Setup(reaction: 10);
        enc.MonsterDamaged = true;
        var rng = new StrictRandomSource();
        var r = PartyOptions.Choose(enc, PartyOption.Greetings, rng, false, 0);
        Assert.Equal(PartyOptionOutcome.GiveOrders, r.Outcome);
    }

    [Fact]
    public void MeleeDamage_SetsMonsterDamaged()
    {
        var enc = Setup(count: 1);
        enc.Party.Members[0].WeaponDamageA = 50;
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));
        var resolver = new CombatResolver();
        var rng = new SeededDosRandomSource(1);
        for (var i = 0; i < 20 && !enc.MonsterDamaged; i++)
        {
            enc.SetAction(0, CombatAction.Thrust(0));
            if (resolver.ResolveRound(enc, rng).EncounterOver) break;
        }
        Assert.True(enc.MonsterDamaged);
    }

    [Theory]
    [InlineData(false, 0, 69, true)]  // wilderness: 80 − 10·(1 − 0) = 70
    [InlineData(false, 0, 70, false)]
    [InlineData(false, 3, 99, true)]  // 80 − (−20) = 100: no abs here
    [InlineData(true, 0, 69, true)]   // dungeon: 80 − 5·2 members = 70
    [InlineData(true, 0, 70, false)]
    public void Flee_WildernessTerrainOrDungeonPartySize(bool dungeon, int terrain, int roll, bool escapes)
    {
        var enc = Setup(levels: new[] { 1, 1 });
        enc.RewardXp = 30;
        var r = PartyOptions.Choose(enc, PartyOption.Flee, new StrictRandomSource(roll), dungeon, terrain);
        if (escapes)
        {
            Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
            Assert.Equal(0, r.End!.RewardGold);
            Assert.Equal(30, r.End.RewardXp);
        }
        else
        {
            Assert.Equal(PartyOptionOutcome.LoseRound, r.Outcome);
            Assert.All(enc.Prep, p => Assert.Equal(CombatAction.Cleared, p.PackedAction));
        }
    }

    [Fact]
    public void Flee_Failed_UndoesAFailedMonsterRun()
    {
        var enc = Setup();
        enc.MonstersLoseRound = true;
        PartyOptions.Choose(enc, PartyOption.Flee, new StrictRandomSource(99), false, 0);
        Assert.False(enc.MonstersLoseRound);
    }

    [Fact]
    public void Fight_GivesOrdersWithoutRolling()
    {
        var r = PartyOptions.Choose(Setup(), PartyOption.Fight, new StrictRandomSource(), false, 0);
        Assert.Equal(PartyOptionOutcome.GiveOrders, r.Outcome);
    }

    [Fact]
    public void AveragePartyLevel_CountsDeadAndTruncates()
    {
        var party = new Party();
        party.Members.Add(Member(5));
        party.Members.Add(Member(2));
        party.Members.Add(new Character { Name = "DEAD", Level = 4, Status = CharacterStatus.Dead });
        Assert.Equal(3, PartyOptions.AveragePartyLevel(party)); // 11 / 3
    }

    // --- Session wiring ----------------------------------------------------------------------

    [OriginalDataFact]
    public void Session_Beg_EmptiesSackGoldAndLeavesCombat()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Beggar", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.Sack.DepositRewards(5, 300, new[] { 46, 47, 48 });
        session.Party.GoldCarried = 103; // [D✓] s65: bank-withdrawn gold is lost too

        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        session.Combat!.RewardXp = 20;
        var r = session.ChoosePartyOption(PartyOption.BegMercy);

        Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
        Assert.Null(session.Combat);
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Equal(0, session.Sack.Gold);
        Assert.Equal(25, session.Sack.Experience);
        Assert.Equal(new[] { 0, 47 }, session.Sack.ItemIds); // slot 0 left empty, trailing slot 2 trimmed
        Assert.Equal(0, session.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void Session_OneOptionPerRound_AndOneMoraleCheck()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Talker", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);

        Assert.Equal(MoraleOutcome.Stand, session.StartCombatRound()); // no kills yet
        Assert.Throws<InvalidOperationException>(() => session.StartCombatRound());
        Assert.Equal(PartyOptionOutcome.GiveOrders, session.ChoosePartyOption(PartyOption.Fight).Outcome);
        Assert.Throws<InvalidOperationException>(() => session.ChoosePartyOption(PartyOption.Greetings));

        session.AutoMeleeRank0();
        if (!session.ResolveCombatRound().EncounterOver)
            session.ChoosePartyOption(PartyOption.Fight); // next round: allowed again (morale check runs implicitly)
    }
}
