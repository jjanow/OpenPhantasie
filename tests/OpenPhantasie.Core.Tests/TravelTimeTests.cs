using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>P0.3 (s36): travel-time counter <c>[0x63f2]</c>, town-entry aging, night encounter, wilderness inn.</summary>
public class TravelTimeTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>Replays queued values for <c>Next</c>; repeats the last one when empty. Counts draws.</summary>
    private sealed class Scripted(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);
        private int _last;
        public int Draws { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            if (_q.Count > 0)
                _last = _q.Dequeue();
            return Math.Clamp(_last, minInclusive, maxExclusive - 1);
        }

        public int NextDosWord() => Next(0, 32768);
    }

    private static Character Member(string name, CharacterStatus status = CharacterStatus.Ok, int listen = 0) => new()
    {
        Name = name,
        Status = status,
        Stats = new CharacterStats(12, 12, 12, 12, 12, 12),
        HpMax = 20,
        HpCur = 3,
        MagicMax = 10,
        MagicCur = 1,
        MpCeiling = 10,
        Lifespan = 80 * 26,
        AgeClock = 500,
        Skills = new CharacterSkills(0, 0, 0, 0, 0, listen, 0, 0),
    };

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    /// <summary>A cell whose east neighbour holds <paramref name="match"/>; the party stands on the cell itself.</summary>
    private static (int Region, int X, int Y, int Tile) FindStepEast(OverworldTraversal t, Func<int, bool> match)
    {
        foreach (var id in OverworldState.ValidRegionIds)
        {
            var r = t.GetRegion(id);
            for (var y = 0; y < OverworldRegion.Height; y++)
            for (var x = 0; x + 1 < OverworldRegion.Width; x++)
            {
                var here = r.GetNormalized(x, y);
                var next = r.GetNormalized(x + 1, y);
                if (here != OverworldRegion.WallTile && match(next))
                    return (id, x, y, next);
            }
        }
        throw new InvalidOperationException("No such step on the map.");
    }

    // --- TravelClock / rules ---------------------------------------------------

    [Fact]
    public void Add_WrapsMod65536_SaturationIsDeadCode()
    {
        Assert.Equal(1300, TravelClock.Add(1000, 300));
        Assert.Equal(264, TravelClock.Add(65500, 300)); // OV2 0x6528: 16-bit add, the 0xffff clamp never fires
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1000, 3, 0)]        // DOS wilderness inn alone: 3.9 days, ages nobody
    [InlineData(1791, 6, 0)]
    [InlineData(1792, 7, 1)]        // 7 DAY OUT days = one age unit (two weeks)
    [InlineData(65535, 255, 36)]
    public void DaysOut_AndAgeUnits(int time, int days, int ageUnits)
    {
        Assert.Equal(days, TravelClock.DaysOut(time));
        Assert.Equal(ageUnits, TravelClock.AgeUnits(time));
    }

    [OriginalDataFact]
    public void PathStep_D055_IsCheapestTerrain_InShippedJson()
    {
        var path = Data.TravelTime.StepCost(6);
        for (var c = 0; c <= 10; c++)
            if (c != 6)
                Assert.True(path < Data.TravelTime.StepCost(c), $"class {c}");
    }

    [OriginalDataTheory]
    [InlineData(0, 75)]
    [InlineData(1, 150)]
    [InlineData(2, 225)]
    [InlineData(3, 300)]
    [InlineData(4, 60)]
    [InlineData(6, 60)]
    [InlineData(7, 60)]
    [InlineData(10, 60)]
    public void StepCost_IsDosTable_FromJson(int terrainClass, int cost)
    {
        Assert.Equal(cost, TravelTimeRules.Dos.StepCost(terrainClass));
        if (terrainClass != 6)
            Assert.Equal(cost, Data.TravelTime.StepCost(terrainClass));
        Assert.Equal(1000, Data.TravelTime.WildernessInnDos);
    }

    [OriginalDataTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void TryMove_TimeCost_FollowsTerrainClassOfTheNewTile(int terrainClass)
    {
        var t = Data.CreateTraversal();
        var (region, x, y, tile) = FindStepEast(t, n => n / 10 == terrainClass && n != OverworldRegion.WallTile);
        var state = new OverworldState { RegionId = region, X = x, Y = y };
        var result = t.TryMove(state, CardinalDirection.East);
        Assert.NotEqual(OverworldMoveKind.Blocked, result.Kind);
        Assert.Equal(tile, result.Tile);
        Assert.Equal(TravelTimeRules.Dos.StepCost(terrainClass), result.TimeCost);
    }

    [OriginalDataFact]
    public void TryMove_Blocked_AddsNoTime_TownStepAdds60()
    {
        var t = Data.CreateTraversal();
        var (region, x, y, _) = FindStepEast(t, n => n == OverworldRegion.WallTile);
        var blocked = t.TryMove(new OverworldState { RegionId = region, X = x, Y = y }, CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.Blocked, blocked.Kind);
        Assert.Equal(0, blocked.TimeCost);

        var town = t.TryMove(new OverworldState { RegionId = 1, X = 13, Y = 15 }, CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.EnterTown, town.Kind);
        Assert.Equal(60, town.TimeCost);
    }

    // --- Town entry ---------------------------------------------------------------

    [Fact]
    public void ApplyTownEntry_AddsTravelAgeToEveryMember_DeadToo()
    {
        var alive = Member("alive");
        var dead = Member("dead", CharacterStatus.Dead);
        var party = PartyOf(alive, dead);
        Aging.ApplyTownEntry(party, 1, 3 * 1792 + 255);
        Assert.Equal(503, alive.AgeClock);
        Assert.Equal(503, dead.AgeClock);

        Aging.ApplyTownEntry(party, 1, 1791); // under one week of days: nothing
        Assert.Equal(503, alive.AgeClock);
    }

    [OriginalDataFact]
    public void GameSession_StepsAccrueTime_TownEntryAgesAndResets()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99)); // 99: never an encounter
        var m = Member("walker");
        session.Party.Members.Add(m);
        session.DepartGuildToWilderness();
        session.Overworld.RegionId = 1;
        session.Overworld.X = 12;
        session.Overworld.Y = 15;

        var first = session.MoveOverworld(CardinalDirection.East); // (13,15), a text tile next to Pelnor
        Assert.NotEqual(OverworldMoveKind.Blocked, first.Kind);
        Assert.Equal(first.TimeCost, session.TravelTime);

        session.TravelTime = 7 * 256 * 2 - 60; // + the town step's 60 = exactly two age units
        var town = session.MoveOverworld(CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.EnterTown, town.Kind);
        Assert.Equal(GameMode.Overworld, session.Mode); // "A TOWN. ENTER?" first (manual p.12)
        session.AnswerOverworldEntry(enter: true);
        Assert.Equal(GameMode.Town, session.Mode);
        Assert.Equal(502, m.AgeClock);
        Assert.Equal(0, session.TravelTime);
    }

    [OriginalDataFact]
    public void TravelTime_SurvivesSnapshotRoundTrip()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.TravelTime = 4321;
        var json = SessionCodec.WriteJson(session.CaptureSnapshot());

        var restored = GameSessionFactory.Create(Data, new Scripted(99));
        restored.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(4321, restored.TravelTime);
    }

    // --- Night encounter ---------------------------------------------------------

    [Fact]
    public void NightRoll_35OrMore_IsNotANightEncounter()
    {
        var rng = new Scripted(35);
        Assert.Null(NightEncounter.TryRoll(PartyOf(Member("a")), 0, rng));
        Assert.Equal(1, rng.Draws);
    }

    [Fact]
    public void NightRoll_DrawOrder_MonsterRedrawsTwoAndThree()
    {
        // night 34 | r: 40→2 redraw, 60→3 redraw, 80→4 | watch 50·4/100 = 2 | listen 29 < 30 → heard
        var party = PartyOf(Member("a"), Member("b"), Member("c", listen: 30), Member("d"));
        var rng = new Scripted(34, 40, 60, 80, 50, 29);
        var roll = NightEncounter.TryRoll(party, 3, rng)!;
        Assert.Equal(4 + 12 * 3, roll.MonsterId);
        Assert.Equal(2, roll.WatchSlot);
        Assert.True(roll.Heard);
        Assert.Equal(6, rng.Draws);
    }

    [Fact]
    public void NightRoll_DeadWatcher_NoListenRoll_NotHeard()
    {
        var party = PartyOf(Member("dead", CharacterStatus.Dead, listen: 100), Member("b"));
        var rng = new Scripted(0, 0, 0); // night, r = 0, watch slot 0
        var roll = NightEncounter.TryRoll(party, 0, rng)!;
        Assert.Equal(0, roll.MonsterId);
        Assert.False(roll.Heard);
        Assert.Equal(3, rng.Draws);
    }

    [OriginalDataFact]
    public void AfterPrep_NotHeard_EveryOkMemberSleepsExceptTheWatcher()
    {
        var watcher = Member("watch");
        var other = Member("other");
        var dead = Member("dead", CharacterStatus.Dead);
        var party = PartyOf(other, watcher, dead);
        var lines = NightEncounter.ApplyAfterPrep(party, new NightEncounterRoll(0, 1, Heard: false));
        Assert.Equal(CharacterStatus.Sleep, other.Status);
        Assert.Equal(CharacterStatus.Ok, watcher.Status);
        Assert.Equal(CharacterStatus.Dead, dead.Status);
        Assert.Equal(["watch IS ON WATCH"], lines);
    }

    [OriginalDataFact]
    public void AfterPrep_Heard_NobodySleeps_DeadWatcher_EveryoneSleeps()
    {
        var a = Member("a");
        var b = Member("b");
        var heard = NightEncounter.ApplyAfterPrep(PartyOf(a, b), new NightEncounterRoll(0, 0, Heard: true));
        Assert.Equal(["a IS ON WATCH AND HEARS THE MONSTERS"], heard);
        Assert.Equal(CharacterStatus.Ok, b.Status);

        var dead = Member("dead", CharacterStatus.Dead);
        var c = Member("c");
        var none = NightEncounter.ApplyAfterPrep(PartyOf(dead, c), new NightEncounterRoll(0, 0, Heard: false));
        Assert.Empty(none);
        Assert.Equal(CharacterStatus.Sleep, c.Status);
        Assert.Equal(CharacterStatus.Dead, dead.Status);
    }

    [OriginalDataFact]
    public void GameSession_RandomEncounterAtNight_StartsFightWithSleepingParty()
    {
        var t = Data.CreateTraversal();
        // An ordinary step: not wall/town/inn/dungeon/water/special, no text/fixed/plain sub-code.
        var (region, x, y, tile) = FindStepEast(t, n =>
            n / 10 is >= 0 and <= 3 && n % 10 is not (6 or 7 or 8));
        // step roll 5 < 6 | night 0 | r 0 | watch 0 | listen 99 fails | rest (builder) 0
        var session = GameSessionFactory.Create(Data, new Scripted(5, 0, 0, 0, 99, 0));
        var watcher = Member("watch", listen: 50);
        var sleeper = Member("sleeper");
        session.Party.Members.AddRange([watcher, sleeper]);
        session.DepartGuildToWilderness();
        session.Overworld.RegionId = region;
        session.Overworld.X = x;
        session.Overworld.Y = y;

        var result = session.MoveOverworld(CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.RandomEncounter, result.Kind);
        Assert.Equal(GameMode.Combat, session.Mode);
        Assert.Equal(12 * (tile % 10), session.Combat!.FirstMonsterId);
        Assert.Equal(CharacterStatus.Ok, watcher.Status);
        Assert.Equal(CharacterStatus.Sleep, sleeper.Status);
        Assert.Contains(NightEncounter.Banner, session.Log);
        Assert.Contains("watch IS ON WATCH", session.Log);
        Assert.True(session.Combat.Surprise.OpensWithPartyOptions);

        // The sleep survives into round 1: only the combat wake roll (rand0_99 ≤ 16, here 0) can end it.
        session.ResolveCombatRound();
        Assert.Contains(session.Log, l => l.Contains("sleeper AWAKENS"));
    }

    // --- Wilderness inn ------------------------------------------------------------

    [OriginalDataFact]
    public void Tile41_IsTheWildernessInn()
    {
        var t = Data.CreateTraversal();
        var r = t.TryMove(new OverworldState { RegionId = 1, X = 6, Y = 13 }, CardinalDirection.East); // OUT1 (7,13)
        Assert.Equal(OverworldMoveKind.WildernessInn, r.Kind);
        Assert.Equal(41, r.Tile);
        Assert.Equal(60, r.TimeCost);
    }

    [Fact]
    public void Inn_CantAfford_At255_NothingChanges()
    {
        var m = Member("a");
        var party = PartyOf(m);
        party.GoldCarried = 200;
        var sack = new LootSack { Gold = 55 };
        var r = WildernessInn.Stay(party, sack, new Scripted(0));
        Assert.Equal(TownTransactionKind.Failed, r.Kind);
        Assert.Equal(WildernessInn.CantAffordMessage, r.Message);
        Assert.Equal(200, party.GoldCarried);
        Assert.Equal(3, m.HpCur);
        Assert.Equal(500, m.AgeClock);
    }

    [OriginalDataFact]
    public void Inn_Stay_Charges256_RefillsAll_DeadGetNoLine_AgesAllByOne_D008_D047()
    {
        var caster = Member("averylongname");
        caster.CastSinceRest = true;
        caster.MpCeiling = 12; // gap 2: passes when roll < 40 (D-046)
        var sleeper = Member("sleeper", CharacterStatus.Sleep);
        var dead = Member("dead", CharacterStatus.Dead);
        var party = PartyOf(caster, sleeper, dead);
        party.GoldCarried = 200;
        var sack = new LootSack { Gold = 100 };

        var r = WildernessInn.Stay(party, sack, new Scripted(39));

        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal(0, party.GoldCarried);
        Assert.Equal(44, sack.Gold);
        Assert.Equal(11, caster.MagicMax);
        Assert.False(caster.CastSinceRest);
        Assert.Equal((11, 20), (caster.MagicCur, caster.HpCur));
        Assert.Equal((10, 20, CharacterStatus.Sleep), (sleeper.MagicCur, sleeper.HpCur, sleeper.Status));
        Assert.Equal((10, 20, CharacterStatus.Dead), (dead.MagicCur, dead.HpCur, dead.Status)); // D-047 manual
        Assert.All(party.Members, m => Assert.Equal(501, m.AgeClock)); // D-008: +1 age unit, no time
        Assert.Equal("IN THE INN:; averylong GAINS MORE MAGIC; sleeper   IS RESTING", r.Message);
    }

    [Theory]
    [InlineData(0, 10)] // D-046 manual: DOS <= let roll 0 pass at gap 0; the remaster never goes past the table
    [InlineData(1, 10)]
    public void Inn_GapZero_NeverRaisesPastTable_D046(int roll, int expectedMax)
    {
        var caster = Member("c");
        caster.CastSinceRest = true;
        var party = PartyOf(caster);
        party.GoldCarried = 256;
        WildernessInn.Stay(party, new LootSack(), new Scripted(roll));
        Assert.Equal(expectedMax, caster.MagicMax);
    }

    [OriginalDataFact]
    public void GameSession_StayAtWildernessInn_OnlyOnTile41_AddsNoTravelTime()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        var m = Member("a");
        session.Party.Members.Add(m);
        session.Party.GoldCarried = 300;
        session.DepartGuildToWilderness();
        session.Overworld.RegionId = 1;
        session.Overworld.X = 6;
        session.Overworld.Y = 13;
        Assert.Throws<InvalidOperationException>(() => session.StayAtWildernessInn());

        session.MoveOverworld(CardinalDirection.East);
        var before = session.TravelTime;
        var r = session.StayAtWildernessInn();
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal(before, session.TravelTime);
        Assert.Equal(501, m.AgeClock);
        Assert.Equal(44, session.Party.GoldCarried);
    }
}
