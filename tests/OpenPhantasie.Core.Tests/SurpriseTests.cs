using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Surprise globals <c>[0x649e]</c>/<c>[0x64a0]</c> (<c>combat-resolution.md</c> §Surprise, s33).</summary>
public class SurpriseTests
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

    /// <summary>Always the same roll; counts the draws.</summary>
    private sealed class ConstantRandomSource(int value) : IRandomSource
    {
        public int Draws { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            return Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    private static Character Member(string name) => new()
    {
        Name = name,
        Status = CharacterStatus.Ok,
        Class = CharacterClass.Fighter,
        Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        Skills = new CharacterSkills(200, 0, 0, 0, 0, 0, 0, 0),
        Level = 1,
        HpCur = 500,
        HpMax = 500,
        WeaponDamageA = 5,
    };

    private static Monster Mon(string name = "M") =>
        new() { Name = name, HitPoints = 500, AttackPower = 100, DamageField = 10, Level = 0 };

    private static (CombatEncounter Enc, Character Hero, Monster Mon) Fight(SurpriseFlags surprise, bool opensWithoutOrders)
    {
        var party = new Party();
        var hero = Member("HERO");
        party.Members.Add(hero);
        var enc = new CombatEncounter(party) { MonsterLevel = 0 };
        var mon = Mon();
        enc.AddRank(mon);
        enc.Prepare();
        enc.Surprise = surprise;
        enc.OpensWithoutOrders = opensWithoutOrders;
        return (enc, hero, mon);
    }

    private static bool MonsterActed(CombatResult round) =>
        round.Log.Any(l => l.StartsWith("M HITS", StringComparison.Ordinal) || l.StartsWith("M MISSES", StringComparison.Ordinal));

    [Theory]
    [InlineData(true, 1, true)]   // normal random encounter
    [InlineData(true, 0, false)]  // party surprised
    [InlineData(false, 0, false)] // stale 0,0 — same as party surprised
    [InlineData(false, 1, false)] // stalled
    [InlineData(false, 2, false)] // monsters surprised
    public void PartyOptions_OpenTheFight_OnlyWhenBothFlagsSet(bool firstRound, int hold, bool options)
    {
        Assert.Equal(options, new SurpriseFlags(firstRound, hold).OpensWithPartyOptions);
        Assert.Equal(hold == 2, new SurpriseFlags(firstRound, hold).MonstersStartAsleep);
    }

    [OriginalDataFact]
    public void Normal_MonstersSitOutPassOne_ThenTheHoldIsGone()
    {
        // Monster readiness at L = 0 with roll 99: 693 ≤ 50p² fails in passes 1 and 3, passes in 5.
        // The hold skips the pass-1 readiness roll, so exactly one draw fewer; everything else is identical.
        var (bare, _, _) = Fight(SurpriseFlags.None, opensWithoutOrders: false);
        var (normal, _, _) = Fight(SurpriseFlags.Normal, opensWithoutOrders: false);
        var r1 = new ConstantRandomSource(99);
        var r2 = new ConstantRandomSource(99);

        new CombatResolver().ResolveRound(bare, r1);
        var round = new CombatResolver().ResolveRound(normal, r2);

        Assert.Equal(r1.Draws - 1, r2.Draws);
        Assert.True(MonsterActed(round));                         // pass 5
        Assert.Equal(new SurpriseFlags(false, 0), normal.Surprise); // F cleared at round top, H after pass 1
    }

    [Fact]
    public void Normal_MonsterReadyInPassOne_StillWaitsForPassThree()
    {
        // Roll 0: a monster would act in pass 1. With the hold it acts in pass 3, after the hero's pass-1 swing.
        var (enc, _, _) = Fight(SurpriseFlags.Normal, opensWithoutOrders: false);
        enc.SetAction(0, CombatAction.Thrust(0));
        var round = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));

        var heroLine = round.Log.ToList().FindIndex(l => l.StartsWith("HERO", StringComparison.Ordinal));
        var monLine = round.Log.ToList().FindIndex(l => l.StartsWith("M ", StringComparison.Ordinal));
        Assert.True(heroLine >= 0 && monLine > heroLine);
        Assert.Equal(0, enc.Surprise.MonsterHold);
    }

    [OriginalDataFact]
    public void PartySurprised_OrdersCleared_MonstersActFromPassOne()
    {
        var (enc, hero, mon) = Fight(SurpriseFlags.PartySurprised, opensWithoutOrders: true);
        enc.SetAction(0, CombatAction.Thrust(0)); // even if a Client collected orders

        var round = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));

        Assert.Equal(500, mon.HitPoints);       // the party lost the round
        Assert.True(MonsterActed(round));
        Assert.False(enc.OpensWithoutOrders);   // one-shot
        Assert.Equal(SurpriseFlags.None, enc.Surprise);
        Assert.Equal(CombatAction.Cleared, enc.Prep[0].PackedAction);
        Assert.True(hero.HpCur < 500);
    }

    [OriginalDataFact]
    public void Stalled_FirstRoundIsEmpty_SecondRoundNormal()
    {
        var (enc, _, mon) = Fight(SurpriseFlags.Stalled, opensWithoutOrders: true);
        enc.SetAction(0, CombatAction.Thrust(0));

        var round1 = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));
        Assert.Equal(500, mon.HitPoints);
        Assert.False(MonsterActed(round1));
        Assert.Equal(SurpriseFlags.None, enc.Surprise); // hold 1 → 0 at round end

        enc.SetAction(0, CombatAction.Thrust(0));
        var round2 = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));
        Assert.True(mon.HitPoints < 500);
        Assert.True(MonsterActed(round2));
    }

    /// <summary>A record for the builder: one monster (factor 0), no companions or next group, HP 400.</summary>
    private static Monster Record() => new()
    {
        Name = "M", AttackPower = 100, DamageField = 10, HpBase = 400, Companions = new[] { 0, 0 },
    };

    [OriginalDataFact]
    public void MonstersSurprised_BuiltAsleep_HeldForTwoRounds()
    {
        var party = new Party();
        party.Members.Add(Member("HERO"));
        // Rolls: count, HP, companion, next group (stop). No roll for the asleep state.
        var buildRng = new StrictRandomSource(0, 50, 0, 99);
        var enc = EncounterBuilder.Build(party, 0, _ => Record(), buildRng, surprise: SurpriseFlags.MonstersSurprised);
        Assert.Equal(0, buildRng.Remaining);
        enc.Prepare();
        var mon = enc.AllMonsters.Single();
        var hp = mon.HitPoints;
        Assert.Equal(MonsterStatusFlags.Asleep, mon.StatusFlags); // state 3 − 2 = 1
        Assert.True(enc.OpensWithoutOrders);

        // Round 1: no orders; the sleeper rolls to wake each pass (99 ≥ 15, so it stays asleep).
        enc.SetAction(0, CombatAction.Thrust(0));
        var r1 = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(99));
        Assert.False(MonsterActed(r1));
        Assert.Equal(hp, mon.HitPoints);
        Assert.Equal(MonsterStatusFlags.Asleep, mon.StatusFlags);
        Assert.Equal(1, enc.Surprise.MonsterHold); // 2 → 1

        // Round 2: the party fights; a monster that woke is still held.
        mon.StatusFlags = MonsterStatusFlags.None;
        enc.SetAction(0, CombatAction.Thrust(0));
        var r2 = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));
        Assert.False(MonsterActed(r2));
        Assert.True(mon.HitPoints < hp);
        Assert.Equal(0, enc.Surprise.MonsterHold);

        // Round 3: the hold is gone.
        var r3 = new CombatResolver().ResolveRound(enc, new ConstantRandomSource(0));
        Assert.True(MonsterActed(r3));
    }

    [Fact]
    public void Build_WithoutSurprise_HasNoOpeningEffect()
    {
        var enc = EncounterBuilder.Build(new Party(), 0, _ => Record(), new StrictRandomSource(0, 50, 0, 99));
        Assert.False(enc.OpensWithoutOrders);
        Assert.Equal(SurpriseFlags.None, enc.Surprise);
        Assert.Equal(MonsterStatusFlags.None, enc.AllMonsters.Single().StatusFlags);
    }

    [Fact]
    public void FailedFlee_ReleasesTheHold()
    {
        var (enc, _, _) = Fight(SurpriseFlags.Normal, opensWithoutOrders: false);
        var r = PartyOptions.Choose(enc, PartyOption.Flee, new StrictRandomSource(99), false, 0);
        Assert.Equal(PartyOptionOutcome.LoseRound, r.Outcome);
        Assert.Equal(new SurpriseFlags(true, 0), enc.Surprise); // 0x3d9f: H := 0; F still set → party loses the round
    }

    [Fact]
    public void DispellRunHold_WritesOne_EvenOverTwo()
    {
        var (enc, _, _) = Fight(SurpriseFlags.MonstersSurprised, opensWithoutOrders: false);
        enc.MonstersFrozen = true; // mov [0x64a0], 1
        Assert.Equal(1, enc.Surprise.MonsterHold);
    }

    [Theory]
    [InlineData(90, true, 1)]  // rand ≤ 90 → normal
    [InlineData(91, true, 0)]  // rand > 90 → party surprised
    public void ScriptFight_Op5(int roll, bool firstRound, int hold) =>
        Assert.Equal(new SurpriseFlags(firstRound, hold), Surprise.ScriptFight(new StrictRandomSource(roll)));

    [Theory]
    [InlineData(50, 99, true, false, 1)]  // H: 50 ≤ 50 → 1; F: 99 > 50 and heard → 0
    [InlineData(51, 99, true, false, 0)]
    [InlineData(51, 50, true, true, 0)]   // F: 50 is not > 50 → 1
    [InlineData(50, 99, false, true, 1)]  // nobody heard → F stays 1
    public void LockAmbush_TwoRolls_HoldFirst(int holdRoll, int flagRoll, bool heard, bool firstRound, int hold)
    {
        var rng = new StrictRandomSource(holdRoll, flagRoll);
        Assert.Equal(new SurpriseFlags(firstRound, hold), Surprise.LockAmbush(rng, heard));
        Assert.Equal(0, rng.Remaining);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    public void HeardThenFight_SurprisesThemOn30Plus(int roll, bool surprised) =>
        Assert.Equal(surprised ? SurpriseFlags.MonstersSurprised : SurpriseFlags.Normal,
            Surprise.HeardThenFight(new StrictRandomSource(roll)));

    [Theory]
    [InlineData(79, false)]
    [InlineData(80, true)]
    public void HeardThenRun_FightsOn80Plus(int roll, bool fights) =>
        Assert.Equal(fights, Surprise.HeardThenRun(new StrictRandomSource(roll)) is not null);

    [Theory]
    [InlineData(43, false)]
    [InlineData(44, true)]
    public void BlackKnightsRun_FightsOn44Plus(int roll, bool fights) =>
        Assert.Equal(fights, Surprise.BlackKnightsRun(new StrictRandomSource(roll)) is not null);

    // --- GameSession ---------------------------------------------------------------------------------------------

    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();

    private static GameSession PartyInWilderness(int seed)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        return session;
    }

    [OriginalDataFact]
    public void Session_DefaultsToNormal_OptionsFirst()
    {
        var session = PartyInWilderness(5);
        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        session.BeginWildernessCombat(goblin.Id);
        Assert.Equal(SurpriseFlags.Normal, session.Combat!.Surprise);
        Assert.True(session.CombatRoundHasOptions);
    }

    [OriginalDataFact]
    public void Session_SurprisedOpening_RefusesOptions_ThenRoundTwoHasThem()
    {
        var session = PartyInWilderness(5);
        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        session.BeginWildernessCombat(goblin.Id, SurpriseFlags.PartySurprised);

        Assert.False(session.CombatRoundHasOptions);
        Assert.Throws<InvalidOperationException>(() => session.StartCombatRound());
        Assert.Throws<InvalidOperationException>(() => session.ChoosePartyOption(PartyOption.Fight));

        var round = session.ResolveCombatRound();
        if (!round.EncounterOver)
            Assert.True(session.CombatRoundHasOptions);
    }

    [OriginalDataFact]
    public void Session_FightToEnd_HandlesASurprisedOpening()
    {
        var session = PartyInWilderness(7);
        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        session.BeginWildernessCombat(goblin.Id, SurpriseFlags.MonstersSurprised);
        var end = session.FightToEnd();
        Assert.True(end.EncounterOver);
    }

    [OriginalDataFact]
    public void Snapshot_KeepsSurpriseState()
    {
        var session = PartyInWilderness(3);
        var goblin = Data.Monsters.First(m => m.HpBase <= 8);
        session.BeginWildernessCombat(goblin.Id, SurpriseFlags.MonstersSurprised);

        var snap = session.CaptureSnapshot();
        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));

        Assert.Equal(SurpriseFlags.MonstersSurprised, loaded.Combat!.Surprise);
        Assert.True(loaded.Combat.OpensWithoutOrders);
        Assert.All(loaded.Combat.AllMonsters, m => Assert.Equal(MonsterStatusFlags.Asleep, m.StatusFlags));
    }

    [OriginalDataFact]
    public void BlackKnights_InDng9_UseFactorFour()
    {
        // Factor 4 lets the count roll reach 4 knights (rand·4/102 = 3 for rand ≥ 77); the record's 3 caps at 3.
        var max9 = 0;
        var maxElsewhere = 0;
        for (var seed = 1; seed <= 60; seed++)
        {
            max9 = Math.Max(max9, KnightsInFirstGroup(seed, level: 9));
            maxElsewhere = Math.Max(maxElsewhere, KnightsInFirstGroup(seed, level: 1));
        }
        Assert.Equal(4, max9);
        Assert.Equal(3, maxElsewhere);
    }

    private static int KnightsInFirstGroup(int seed, int level)
    {
        var session = PartyInWilderness(seed);
        var lvl = Data.CreateDungeonTraversal().GetLevel(level);
        var (x, y) = FindWalkable(lvl);
        session.EnterDungeon(level, x, y);
        session.BeginDungeonCombat(EncounterBuilder.BlackKnightId);
        return session.Combat!.Ranks.First(r => r.RankIndex == 0).Members.Count;
    }

    private static (int X, int Y) FindWalkable(DungeonLevel level)
    {
        for (var y = 0; y < DungeonLevel.Height; y++)
        for (var x = 0; x < DungeonLevel.Width; x++)
        {
            if (level.IsWalkable(x, y))
                return (x, y);
        }
        throw new InvalidOperationException("No walkable cell.");
    }
}
