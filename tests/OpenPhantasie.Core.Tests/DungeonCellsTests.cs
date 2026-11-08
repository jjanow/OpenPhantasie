using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// P0.4 (s39): dungeon fight cells (OV2 0x2249), the 0x2a26 cell rewrite, BEG/FLEE step back, traps 0x2f8e, locks 0x2e03,
/// swimming 0x685c and @90 at creation — all [C] static (dungeon-representation.md, overworld-movement.md).
/// </summary>
public partial class DungeonCellsTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>Returns queued rolls (as given) for every draw, then falls back to a seeded source.</summary>
    private sealed class Rolls(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);
        private readonly SeededDosRandomSource _rest = new(1);
        public int Next(int minInclusive, int maxExclusive) =>
            _q.Count > 0 ? _q.Dequeue() : _rest.Next(minInclusive, maxExclusive);
        public int NextDosWord() => _q.Count > 0 ? _q.Dequeue() : _rest.NextDosWord();
        public int Left => _q.Count;

        /// <summary>Queue more rolls (after a session's set-up has drawn what it needs).</summary>
        public Rolls Then(params int[] more)
        {
            foreach (var v in more)
                _q.Enqueue(v);
            return this;
        }
    }

    private static Character Member(string name, int spot = 0, int disarm = 0, int listen = 0, int pick = 0,
        int swim = 0, int dex = 10, int hp = 30) => new()
    {
        Name = name,
        Status = CharacterStatus.Ok,
        Class = CharacterClass.Thief,
        Stats = new CharacterStats(10, 10, dex, 10, 10, 10),
        Skills = new CharacterSkills(50, 0, 0, spot, disarm, listen, pick, swim),
        Level = 1,
        HpCur = hp,
        HpMax = hp,
        SpellsKnown = new int[60],
    };

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    /// <summary>A 33×38 level with floor at (0,0) and <paramref name="cell"/> at (1,0); tier byte 0x4e7 = <paramref name="tier"/>.</summary>
    private static (DungeonTraversal, DungeonState) OneCell(byte cell, int tier = 0, int levelId = 99)
    {
        var buffer = new byte[DungeonLevel.BufferSize];
        buffer[0] = 1;
        buffer[1] = cell;
        buffer[DungeonEncounters.TierOffset] = (byte)tier;
        var level = DungeonLevel.FromBuffer(levelId, "TEST", buffer);
        var traversal = new DungeonTraversal([level]);
        var state = new DungeonState();
        traversal.Enter(state, levelId, 0, 0, level.CloneAsBuffer());
        return (traversal, state);
    }

    // --- Wandering / forced fights -----------------------------------------------------------------------------

    [Fact]
    public void WanderingChance_DropsPerBuild_ThenSticksAtOne()
    {
        var c = DungeonEncounters.ChanceOnEntry;
        var seen = new List<int>();
        for (var i = 0; i < 5; i++)
            seen.Add(c = DungeonEncounters.AfterBuild(c));
        Assert.Equal(new[] { 6, 5, 1, 1, 1 }, seen);
    }

    [OriginalDataTheory]
    [InlineData(7, true)]   // jle: rand0_99 ≤ [0x1a5a] fights
    [InlineData(8, false)]
    public void Wandering_FightsWhenRollAtMostChance(int roll, bool fights)
    {
        var level = Data.CreateDungeonTraversal().GetLevel(1);
        var m = DungeonEncounters.RollWandering(level, 1, 8, 7, new Rolls(roll, 99));
        Assert.Equal(fights, m is not null);
        if (fights)
            Assert.Equal(3, m); // 99/33 + 8 + T 0 − 8 = 3 (SKUM)
    }

    [OriginalDataFact]
    public void Wandering_Dng6_SecondRollAbove50_IsTrolkin()
    {
        var level = Data.CreateDungeonTraversal().GetLevel(6);
        Assert.Equal(4, DungeonEncounters.Tier(level));
        Assert.Equal(DungeonEncounters.TrolkinMonsterId,
            DungeonEncounters.RollWandering(level, 6, 17, 7, new Rolls(0, 0, 51)));
        Assert.Equal("TROLKIN", Data.Monsters[DungeonEncounters.TrolkinMonsterId].Name);
        Assert.Equal(13, DungeonEncounters.RollWandering(level, 6, 17, 7, new Rolls(0, 0, 50))); // 0 + 17 + 4 − 8
    }

    [OriginalDataFact]
    public void ForcedFight_ReadsTheLevelTable()
    {
        var level = Data.CreateDungeonTraversal().GetLevel(9);
        var id = DungeonEncounters.ForcedFightMonster(level, 0x80);
        Assert.Equal("BLACK KNIGHT", Data.Monsters[id].Name);
    }

    [OriginalDataFact]
    public void WanderingCell_NoFight_ShowsMessageCellPlus3_OnceInARow()
    {
        var dng1 = Data.CreateDungeonTraversal().GetLevel(1);
        var buffer = new byte[DungeonLevel.BufferSize];
        buffer[0] = 1;
        buffer[1] = 0x0c;
        buffer[2] = 0x0c;
        var level = dng1.WithBuffer(buffer); // DNG1's MESS messages over a test grid
        var t = new DungeonTraversal([level]);
        var s = new DungeonState();
        t.Enter(s, 1, 0, 0, level);
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls(8));
        Assert.Equal(DungeonMoveKind.TriggerMessage, r.Kind);
        Assert.Equal(15, r.MessageId);
        Assert.Equal(dng1.Messages[15].Lines[0], r.Message);
        Assert.Null(r.MonsterId);
        var again = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls(8)); // [0x64f4] == 15
        Assert.Equal(DungeonMoveKind.Moved, again.Kind);
    }

    // --- ScriptLow description cells 0x12..0x7f (OV2 0x298b, s41) -----------------------------------------------

    [OriginalDataFact]
    public void ScriptLow_Dng1Data_PointsAtMessageCellMinus0x11()
    {
        var dng1 = Data.CreateDungeonTraversal().GetLevel(1);
        Assert.Equal(0x39, dng1.Get(32, 35));
        Assert.StartsWith("THE CAVERN STOPS HERE", dng1.Messages[0x39 - 0x11].Lines[0]);
    }

    [OriginalDataFact]
    public void ScriptLow_ShowsEveryStep_AndLeavesTheLastMessageAlone()
    {
        var dng1 = Data.CreateDungeonTraversal().GetLevel(1);
        var buffer = new byte[DungeonLevel.BufferSize];
        buffer[0] = 1;
        buffer[1] = 0x9e; // message cell: 0x9e − 0x76 = 40 (0x23c0, sets [0x64f4])
        buffer[2] = 0x39; // ScriptLow:    0x39 − 0x11 = 40 (0x298b → 0x2a95, no check, no write)
        buffer[3] = 0x39;
        buffer[4] = 0x9e;
        var level = dng1.WithBuffer(buffer);
        var t = new DungeonTraversal([level]);
        var s = new DungeonState();
        t.Enter(s, 1, 0, 0, level);
        var party = PartyOf(Member("A"));

        var first = t.TryMove(s, CardinalDirection.East, party, new Rolls());
        Assert.Equal(DungeonMoveKind.TriggerMessage, first.Kind);
        Assert.Equal(40, s.LastMessageId);

        for (var i = 0; i < 2; i++)
        {
            var low = t.TryMove(s, CardinalDirection.East, party, new Rolls()); // same id as the last message: shown anyway
            Assert.Equal(DungeonMoveKind.TriggerMessage, low.Kind);
            Assert.Equal(40, low.MessageId);
            Assert.Equal(dng1.Messages[40].Lines, low.Log);
            Assert.Equal(0x39, s.Level!.Get(s.X, s.Y)); // never rewritten
        }

        var again = t.TryMove(s, CardinalDirection.East, party, new Rolls()); // [0x64f4] still 40 → nothing
        Assert.Equal(DungeonMoveKind.Moved, again.Kind);
    }

    [Fact]
    public void ScriptLow_EmptySlot_ShowsNothing()
    {
        var (t, s) = OneCell(0x39); // a bare test level holds no MESS messages (0x2418 fails → 0x2cfd)
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls());
        Assert.Equal(DungeonMoveKind.Moved, r.Kind);
        Assert.Null(r.Message);
    }

    [Fact]
    public void UnknownMessage_ShowsNothing()
    {
        var (t, s) = OneCell(0x0a); // a bare test level holds no MESS messages
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls(8));
        Assert.Equal(DungeonMoveKind.Moved, r.Kind);
        Assert.Null(r.Message);
    }

    [OriginalDataFact]
    public void EveryLevel_HoldsTheMessagesItsCellsName()
    {
        var traversal = Data.CreateDungeonTraversal();
        foreach (var id in traversal.LevelIds)
        {
            var level = traversal.GetLevel(id);
            foreach (var b in level.Cells.Distinct())
            {
                int? msg = b switch
                {
                    >= 0x08 and <= 0x11 => b + 3,
                    >= 0x8b and <= 0xb8 => b - 0x76,
                    >= 2 and <= 6 => b + 4,
                    _ => null,
                };
                if (msg is int m)
                    Assert.True(level.Messages.ContainsKey(m), $"DNG{id} cell 0x{b:x2} → message {m}");
            }
        }
    }

    [Fact]
    public void WanderingCell_Fight_UsesTierAndNormalSurprise()
    {
        var (t, s) = OneCell(0x0a, tier: 2);
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls(0, 40));
        Assert.Equal(DungeonMoveKind.TriggerEncounter, r.Kind);
        Assert.Equal(40 / 33 + 10 + 2 - 8, r.MonsterId);
        Assert.Equal(SurpriseFlags.Normal, r.Surprise);
        Assert.Null(r.Message); // DOS prints nothing here (MONSTERS APPEAR is script op 17)
    }

    [Theory]
    [InlineData(0x80, 0x8a)]
    [InlineData(0x89, 0x8a)]
    [InlineData(0xb9, 0xb9)]
    [InlineData(0xc5, 0xb9)]
    [InlineData(0xc7, 0xb9)]
    [InlineData(0xcb, 0xc8)]
    [InlineData(0xd6, 0xc8)]
    [InlineData(0x08, 0x08)] // wandering cells stay
    [InlineData(0xdd, 0xdd)]
    public void ClearFightCell_Rewrites0x2a26(byte before, byte after)
    {
        var level = new DungeonLevel(1, "T", new byte[DungeonLevel.GridByteLength]);
        level.Set(3, 4, before);
        DungeonEncounters.ClearFightCell(level, 3, 4);
        Assert.Equal(after, level.Get(3, 4));
    }

    // --- Session: chance, cell clear, step back ----------------------------------------------------------------

    private static GameSession SessionInDng1(IDosRandomSource rng, out Character hero)
    {
        var session = GameSessionFactory.Create(Data, rng);
        session.OpenGuild();
        hero = session.CreateAndEnroll("Crawler", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(1, 20, 0);
        return session;
    }

    /// <summary>Put <paramref name="cell"/> south of (20,0) at (20,1) and step onto it.</summary>
    private static DungeonMoveResult StepOnto(GameSession s, byte cell)
    {
        s.Dungeon!.Level!.Set(20, 1, cell);
        return s.MoveDungeon(CardinalDirection.South);
    }

    [OriginalDataFact]
    public void ForcedFight_Win_ClearsTheCell_AndBuildLowersChance()
    {
        var s = SessionInDng1(new SeededDosRandomSource(3), out var hero);
        hero.HpCur = hero.HpMax = 999;
        var r = StepOnto(s, 0x80);
        Assert.Equal(DungeonMoveKind.TriggerCombat, r.Kind);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(DungeonEncounters.ForcedFightMonster(s.Dungeon!.Level!, 0x80), s.Combat!.FirstMonsterId);
        Assert.Equal(6, s.Dungeon.WanderingChance);
        foreach (var m in s.Combat.AllMonsters)
            m.HitPoints = 0; // the party wins at once
        s.FightToEnd();
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal(0x8a, s.Dungeon.Level!.Get(20, 1));
        Assert.Equal((20, 1), (s.Dungeon.X, s.Dungeon.Y));
    }

    [OriginalDataFact]
    public void Beg_StepsBack_RaisesChance_AndKeepsTheCell()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out _);
        rng.Then(Enumerable.Repeat(0, 200).ToArray()); // XP 0 → the morale check never runs
        StepOnto(s, 0x80);
        Assert.Equal(6, s.Dungeon!.WanderingChance);
        var res = s.ChoosePartyOption(PartyOption.BegMercy);
        Assert.Equal(PartyOptionOutcome.CombatOver, res.Outcome);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal(7, s.Dungeon.WanderingChance); // 0x3c0b: +1
        Assert.Equal((20, 0), (s.Dungeon.X, s.Dungeon.Y)); // 0x29a9
        Assert.Equal(0x80, s.Dungeon.Level!.Get(20, 1));
    }

    [OriginalDataFact]
    public void FleeSuccess_StepsBack_GreetDoesNot()
    {
        // FLEE: rand0_99 < 80 − 5·partySize → roll 0 escapes.
        var rng = new Rolls();
        var s = SessionInDng1(rng, out _);
        rng.Then(Enumerable.Repeat(0, 200).ToArray());
        StepOnto(s, 0x80);
        Assert.Equal(PartyOptionOutcome.CombatOver, s.ChoosePartyOption(PartyOption.Flee).Outcome);
        Assert.Equal((20, 0), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.Equal(0x80, s.Dungeon.Level!.Get(20, 1));

        // GREET (rand0_99 < Σreaction + 10) ends the fight with no step back and no cell clear.
        StepOnto(s, 0x80);
        s.Combat!.ReactionSum = 50;
        Assert.Equal(PartyOptionOutcome.CombatOver, s.ChoosePartyOption(PartyOption.Greetings).Outcome);
        Assert.Equal((20, 1), (s.Dungeon.X, s.Dungeon.Y));
        Assert.Equal(0x80, s.Dungeon.Level.Get(20, 1));
    }

    [OriginalDataFact]
    public void S35Fixture_Dng1LockAmbushWon_BecomesC8()
    {
        // s35 DOSBox: DNGX.SAV = DNG1 except offset 1240 (19,37) 0xCB → 0xC8 after the fight there.
        var s = SessionInDng1(new SeededDosRandomSource(11), out var hero);
        hero.HpCur = hero.HpMax = 999;
        hero.Skills = hero.Skills with { Listen = 0 }; // nobody hears → ambush at once
        var d = s.Dungeon!;
        Assert.Equal(0xcb, d.Level!.Get(19, 37));
        d.X = 19;
        d.Y = 36;
        d.Level.Set(19, 36, 1);
        var r = s.MoveDungeon(CardinalDirection.South);
        Assert.Equal(DungeonMoveKind.TriggerLock, r.Kind);
        Assert.Equal(DungeonLocks.AmbushMonster(d.Level, 0xcb), s.Combat!.FirstMonsterId);
        Assert.Equal(2, s.Combat.FirstMonsterId); // 12·T(0) + 4 − 2
        foreach (var m in s.Combat.AllMonsters)
            m.HitPoints = 0;
        s.FightToEnd();
        Assert.Equal(0xc8, d.Level.Get(19, 37));
        Assert.Equal(0xc8, d.Level.GetBufferByte(1240));
    }

    // --- Traps ------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Trap_Spotted_ByTheLastSucceedingMember_NothingHappensYet()
    {
        var (t, s) = OneCell(3);
        var a = Member("A", spot: 50);
        var b = Member("B", spot: 50);
        var c = Member("C", spot: 10);
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(a, b, c), new Rolls(50, 50, 11)); // ≤ passes, 11 > 10
        Assert.Equal(DungeonMoveKind.TriggerTrap, r.Kind);
        Assert.Equal(1, r.SpotterIndex);
        Assert.Equal("B SPOTS A TRAP", r.Message); // manual p.10 (D-052)
        Assert.Null(r.Damage);
        Assert.Equal(3, t.LevelOf(s).Get(1, 0));
    }

    [Fact]
    public void Trap_RefusedOrNoDisarmer_StepsBack_TrapStays()
    {
        var (t, s) = OneCell(3);
        var party = PartyOf(Member("A", spot: 99));
        t.TryMove(s, CardinalDirection.East, party, new Rolls(0));
        var r = DungeonTraversal.ResolveSpottedTrap(s, t.LevelOf(s), 3, 1, 0, null, party, new Rolls());
        Assert.True(r.SteppedBack);
        Assert.Equal((0, 0), (s.X, s.Y));
        Assert.Equal(3, t.LevelOf(s).Get(1, 0));
    }

    [OriginalDataFact]
    public void Trap_DisarmSucceeds_AtOrBelowSkill_CellBecomesFloor()
    {
        var (t, s) = OneCell(3);
        var party = PartyOf(Member("A", disarm: 40));
        var r = DungeonTraversal.ResolveSpottedTrap(s, t.LevelOf(s), 3, 1, 0, 0, party, new Rolls(40));
        Assert.Equal("A SUCCEEDS", r.Message);
        Assert.Equal(DungeonTraps.SpentCell, t.LevelOf(s).Get(1, 0));
        Assert.Equal(30, party.Members[0].HpCur);
    }

    [OriginalDataFact]
    public void Trap_DisarmFails_ThenSprings()
    {
        var (t, s) = OneCell(3);
        var party = PartyOf(Member("A", disarm: 40, dex: 0));
        // 41 > 40 fails; avoid roll 0·22 < 0 is false; damage 99·(3−1)·5/100 = 9
        var r = DungeonTraversal.ResolveSpottedTrap(s, t.LevelOf(s), 3, 1, 0, 0, party, new Rolls(41, 0, 99));
        Assert.Equal(new[] { "A FAILS", "IN THE TRAP:", "A         TAKES 9  DAMAGE" }, r.Log);
        Assert.Equal(21, party.Members[0].HpCur);
        Assert.Equal(DungeonTraps.SpentCell, t.LevelOf(s).Get(1, 0));
    }

    [Fact]
    public void DisarmWithASleepingMember_IsRefused()
    {
        var (t, s) = OneCell(3);
        var sleeper = Member("Z");
        sleeper.Status = CharacterStatus.Sleep;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DungeonTraversal.ResolveSpottedTrap(s, t.LevelOf(s), 3, 1, 0, 0, PartyOf(sleeper), new Rolls()));
    }

    [OriginalDataFact]
    public void TrapSpring_AvoidRoll_OnlyForOk_ZeroDamageStillCostsOneHp()
    {
        var ok = Member("OK", dex: 10);         // a = 2: avoid when 22·rand < 10·10·5 = 500 → rand ≤ 22
        var sleeper = Member("SL", dex: 18);
        sleeper.Status = CharacterStatus.Sleep;  // no avoid roll
        var dead = Member("DD");
        dead.Status = CharacterStatus.Dead;
        var party = PartyOf(ok, sleeper, dead);
        var log = new List<string>();
        var rng = new Rolls(22, 10); // OK avoids (484 < 500); SL damage 10·1·5/100 = 0 → floor 1
        Assert.False(DungeonTraps.Spring(party, 2, rng, log, out var total));
        Assert.Equal(new[] { "IN THE TRAP:", "OK        AVOIDS THE TRAP", "SL        AVOIDS THE TRAP" }, log);
        Assert.Equal(30, ok.HpCur);
        Assert.Equal(29, sleeper.HpCur);
        Assert.Equal(1, total);
        Assert.Equal(0, rng.Left);
    }

    [OriginalDataFact]
    public void TrapSpring_Death_LowersCon_AndAllDeadIsDefeat()
    {
        var hero = Member("H", dex: 0, hp: 1);
        var party = PartyOf(hero);
        var log = new List<string>();
        Assert.True(DungeonTraps.Spring(party, 6, new Rolls(99, 50), log, out _));
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(8, hero.Stats.Con); // D-009: CON 10, LUCK 10 < 11 → −2
        Assert.Equal("H         DIES", log[^1]);
    }

    [Fact]
    public void Trap_Unspotted_SpringsAtOnce()
    {
        var (t, s) = OneCell(5);
        var party = PartyOf(Member("A", spot: 0, dex: 0));
        // spot 1 > 0 fails; avoid 0 < 0 false; damage 50·4·5/100 = 10
        var r = t.TryMove(s, CardinalDirection.East, party, new Rolls(1, 0, 50));
        Assert.Null(r.SpotterIndex);
        Assert.Equal(10, r.Damage);
        Assert.Equal(9, r.MessageId); // MESS a + 4
        Assert.Equal(DungeonTraps.SpentCell, t.LevelOf(s).Get(1, 0));
    }

    [OriginalDataFact]
    public void Session_TrapPrompt_DisarmAndStepBack()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { SpotTrap = 50, DisarmTrap = 0 };
        rng.Then(50);
        var r = StepOnto(s, 4);
        Assert.Equal(0, r.SpotterIndex);
        var prompt = s.DungeonTrapPrompt!;
        Assert.Equal(4, prompt.TrapKind);
        Assert.Equal(new[] { 0 }, prompt.Disarmers);
        var back = s.AnswerDungeonTrap(null);
        Assert.True(back.SteppedBack);
        Assert.Null(s.DungeonTrapPrompt);
        Assert.Equal((20, 0), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.Equal(4, s.Dungeon.Level!.Get(20, 1));
    }

    [OriginalDataFact]
    public void SessionPrompts_TrapDisarmAsksWhoThenDisarms()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { SpotTrap = 50, DisarmTrap = 0 };
        rng.Then(50);
        StepOnto(s, 4);
        var p = new SessionPrompts(s);
        Assert.Equal(SessionPromptKind.DungeonTrap, p.Current!.Kind);
        Assert.Equal([SessionPrompts.Disarm, SessionPrompts.Leave], p.Current.Options); // D-052: D)ISARM OR L)EAVE
        Assert.Equal("DL", p.Current.Hotkeys);
        Assert.False(p.Current.IsYesNo);
        p.Answer(1); // D
        Assert.Equal(SessionPromptKind.DungeonTrapWho, p.Current!.Kind);
        Assert.Equal([hero.Name], p.Current.Options);
        p.Answer(1);
        Assert.Null(s.DungeonTrapPrompt);
        Assert.Null(p.Current);
    }

    [OriginalDataFact]
    public void SessionPrompts_TrapLeaveStepsBack()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { SpotTrap = 50 };
        rng.Then(50);
        StepOnto(s, 4);
        var p = new SessionPrompts(s);
        p.Answer(2); // L)EAVE
        Assert.Null(s.DungeonTrapPrompt);
        Assert.Equal((20, 0), (s.Dungeon!.X, s.Dungeon.Y));
    }

    // --- Locks ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0xb9, DungeonDoorKind.Open)]
    [InlineData(0xc8, DungeonDoorKind.Open)]
    [InlineData(0xba, DungeonDoorKind.Ambush)]
    [InlineData(0xc5, DungeonDoorKind.Ambush)]
    [InlineData(0xcb, DungeonDoorKind.Ambush)]
    [InlineData(0xc6, DungeonDoorKind.Locked)]
    [InlineData(0xd5, DungeonDoorKind.Locked)]
    [InlineData(0xc7, DungeonDoorKind.Jammed)]
    [InlineData(0xd6, DungeonDoorKind.Jammed)]
    public void DoorKinds(byte cell, DungeonDoorKind kind) => Assert.Equal(kind, DungeonLocks.Classify(cell));

    [OriginalDataFact]
    public void Lock_Picked_OpensAndStays()
    {
        var (t, s) = OneCell(0xd5);
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A", pick: 30), Member("B", pick: 0)), new Rolls(29, 0));
        Assert.Equal("A PICKS THE LOCK", r.Message);
        Assert.Equal(0xc8, t.LevelOf(s).Get(1, 0));
        Assert.Equal((1, 0), (s.X, s.Y));
    }

    [Fact]
    public void OpenDoor_DoesNothing()
    {
        var (t, s) = OneCell(0xb9);
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A")), new Rolls());
        Assert.Equal(DungeonMoveKind.Moved, r.Kind);
    }

    [Fact]
    public void Ambush_NotHeard_FightsWithLockRolls()
    {
        var (t, s) = OneCell(0xbc, tier: 1); // di = 4 → monster 12 + 4 − 2 = 14
        var r = t.TryMove(s, CardinalDirection.East, PartyOf(Member("A", listen: 10)), new Rolls(10, 51, 51));
        Assert.Equal(14, r.MonsterId);
        Assert.Equal(new SurpriseFlags(true, 0), r.Surprise); // H: 51 > 50 → 0; F stays 1 (nobody heard)
    }

    private static GameSession RoundTrip(GameSession s)
    {
        var copy = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        copy.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        return copy;
    }

    [OriginalDataFact]
    public void TrapAndLockPrompts_SurviveASave()
    {
        // s41: modal prompts are kept in the JSON save (P2 "prompts in snapshots").
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { SpotTrap = 50 };
        rng.Then(50);
        StepOnto(s, 4);
        var trap = s.DungeonTrapPrompt!;
        var loaded = RoundTrip(s).DungeonTrapPrompt!;
        Assert.Equal((trap.TrapKind, trap.X, trap.Y, trap.SpotterIndex, trap.Message),
            (loaded.TrapKind, loaded.X, loaded.Y, loaded.SpotterIndex, loaded.Message));
        Assert.Equal(trap.Disarmers, loaded.Disarmers);

        var rng2 = new Rolls();
        var s2 = SessionInDng1(rng2, out var hero2);
        hero2.Skills = hero2.Skills with { Listen = 50 };
        rng2.Then(49, 0, 0);
        StepOnto(s2, 0xbc);
        Assert.Equal(s2.DungeonLockPrompt, RoundTrip(s2).DungeonLockPrompt);
    }

    [OriginalDataFact]
    public void Session_LockPrompt_NoStartsTheAmbush()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { Listen = 50 };
        rng.Then(49, 0, 0);
        StepOnto(s, 0xbc);
        Assert.NotNull(s.DungeonLockPrompt);
        s.AnswerDungeonLock(goBack: false);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(2, s.Combat!.FirstMonsterId);
    }

    [OriginalDataFact]
    public void SessionPrompts_LockNoStartsTheAmbush()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { Listen = 50 };
        rng.Then(49, 0, 0);
        StepOnto(s, 0xbc);
        var p = new SessionPrompts(s);
        Assert.Equal(SessionPromptKind.DungeonLock, p.Current!.Kind);
        p.Answer(yes: false);
        Assert.Equal(GameMode.Combat, s.Mode);
    }

    // --- Script op 17 / op 16 ---------------------------------------------------------------------------------

    /// <summary>Rewrite DNG1 event record 1 (feature 0xdd) to run script 0 = [op, a, 0, 0] and put it at (20,1).</summary>
    private static void PlantEvent(GameSession s, byte op, byte a)
    {
        var level = s.Dungeon!.Level!;
        var rec = DungeonScript.EventRecordOffset(0xdd);
        level.SetBufferByte(rec, 0x7e);      // repeatable, message id 126 (none)
        level.SetBufferByte(rec + 2, 0);     // choice 1 → script 0
        level.SetBufferByte(DungeonScript.ScriptBase, op);
        level.SetBufferByte(DungeonScript.ScriptBase + 1, a);
        level.Set(20, 1, 0xdd);
    }

    [OriginalDataFact]
    public void ScriptOp17_OpensWithoutPartyOptions()
    {
        var s = SessionInDng1(new SeededDosRandomSource(2), out _);
        PlantEvent(s, 17, 5);
        s.MoveDungeon(CardinalDirection.South);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(5, s.Combat!.FirstMonsterId);
        Assert.False(s.CombatRoundHasOptions);
    }

    [OriginalDataFact]
    public void ScriptOp16_SpottedTrap_PromptsOnTheEventCell()
    {
        var rng = new Rolls();
        var s = SessionInDng1(rng, out var hero);
        hero.Skills = hero.Skills with { SpotTrap = 50 };
        rng.Then(50);
        PlantEvent(s, 16, 4);
        s.MoveDungeon(CardinalDirection.South);
        var p = s.DungeonTrapPrompt!;
        Assert.Equal((4, 20, 1), (p.TrapKind, p.X, p.Y));
    }

    // --- Swimming / @90 ---------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Swimming_SwimsOrSinks1To5()
    {
        var a = Member("A", swim: 30);
        var b = Member("B", swim: 30);
        var c = Member("C", swim: 30, hp: 1);
        var log = new List<string>();
        // A: 29 < 30 swims. B: 30 fails, d = (99·5 + 100)/100 = 5. C: fails, d = 1 → dies.
        Assert.False(Swimming.Resolve(PartyOf(a, b, c), new Rolls(29, 30, 99, 30, 0), log));
        Assert.Equal(new[] { "SWIMMING:", "A         SWIMS", "B         SINKS AND TAKES 5   DAMAGE", "C         DIES" }, log);
        Assert.Equal(25, b.HpCur);
        Assert.Equal(CharacterStatus.Dead, c.Status);
    }

    [OriginalDataFact]
    public void WaterStep_IsASwim_AndALastDeathIsDefeat()
    {
        var traversal = Data.CreateTraversal();
        foreach (var region in OverworldState.ValidRegionIds)
        {
            var map = traversal.GetRegion(region);
            for (var y = 1; y < 20; y++)
            for (var x = 0; x < 25; x++)
            {
                int tile, from;
                try { tile = map.GetNormalized(x, y); from = map.GetNormalized(x, y - 1); }
                catch (ArgumentOutOfRangeException) { continue; }
                if (tile / 10 != 10 || tile == 100 || from / 10 == 10 || from == 100 || from % 10 != 0
                    || from / 10 is 5 or 7 or 11 || from is 40 or 41)
                    continue;
                var rng = new Rolls();
                var s = GameSessionFactory.Create(Data, rng);
                s.OpenGuild();
                var hero = s.CreateAndEnroll("Swimmer", Race.Human, CharacterClass.Fighter);
                Assert.True(s.TryAddToParty(hero, out _));
                s.DepartGuildToWilderness();
                (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (region, x, y - 1);
                hero.HpCur = 1;
                rng.Then(hero.Skills.Swim, 0); // fails the swim (strict <), 1 damage
                var r = s.MoveOverworld(CardinalDirection.South);
                Assert.Equal(OverworldMoveKind.Swim, r.Kind);
                Assert.Equal(CharacterStatus.Dead, hero.Status);
                Assert.True(s.PartyDefeatPending);
                return;
            }
        }
        Assert.Fail("No water tile found next to a plain tile.");
    }

    [OriginalDataFact]
    public void Creation_Writes90AsOne()
    {
        var hero = CharacterCreation.Create(
            new CharacterCreationRequest { Name = "P", Race = Race.Human, Class = CharacterClass.Fighter },
            Data.CreationTables, new SeededDosRandomSource(1));
        Assert.Equal(1, hero.Phantasie1.Word90);
    }

    [OriginalDataFact]
    public void Snapshot_KeepsChancePrevCellAndLastMessage()
    {
        var s = SessionInDng1(new SeededDosRandomSource(4), out _);
        var d = s.Dungeon!;
        (d.PrevX, d.PrevY, d.WanderingChance, d.LastMessageId) = (19, 0, 1, 13);
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        var t = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        t.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal((19, 0, 1, 13), (t.Dungeon!.PrevX, t.Dungeon.PrevY, t.Dungeon.WanderingChance, t.Dungeon.LastMessageId));
    }
}
