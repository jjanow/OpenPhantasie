using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// P0.3 (s39): class-7 one-time fight <c>0x6094</c> / <c>[0x6496]</c>, one-shot fixed encounters (x7 → x8, monster = arg),
/// stale surprise globals, the day branches of <c>0x70ed</c> (<c>0x723b..0x74f0</c>) and their persistence.
/// </summary>
public class WildernessEncounterTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

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

    private static Character Member(string name, CharacterStatus status = CharacterStatus.Ok, int listen = 0, int score = 0) =>
        new()
        {
            Name = name,
            Status = status,
            Stats = new CharacterStats(12, 12, 12, 12, 12, 12),
            HpMax = 20,
            HpCur = 20,
            MagicMax = 10,
            MagicCur = 1,
            MpCeiling = 10,
            Lifespan = 80 * 26,
            AgeClock = 500,
            Score = score,
            Skills = new CharacterSkills(0, 0, 0, 0, 0, listen, 0, 0),
        };

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    /// <summary>A non-wall cell whose east neighbour (same region) satisfies <paramref name="match"/>.</summary>
    private static (int Region, int X, int Y) FindStepEast(OverworldTraversal t, Func<int, int, int, bool> match)
    {
        foreach (var id in OverworldState.ValidRegionIds)
        {
            var r = t.GetRegion(id);
            for (var y = 0; y < OverworldRegion.Height; y++)
            for (var x = 0; x + 1 < OverworldRegion.Width; x++)
            {
                if (r.GetNormalized(x, y) is OverworldRegion.WallTile or 40 or 41 || r.GetNormalized(x, y) / 10 == 5)
                    continue;
                if (match(id, x + 1, y))
                    return (id, x, y);
            }
        }
        throw new InvalidOperationException("No such step on the map.");
    }

    private static OverworldState At((int Region, int X, int Y) c) => new() { RegionId = c.Region, X = c.X, Y = c.Y };

    // --- Class 7 (0x6094) --------------------------------------------------------------------------

    [OriginalDataFact]
    public void TileClassFightMonster_Is12TPlus9_ZeroBased()
    {
        Assert.Equal(57, OverworldTraversal.TileClassFightMonster(4));
        Assert.Equal(69, OverworldTraversal.TileClassFightMonster(5));
        Assert.Equal("WARRIOR", Data.GetMonster(57).Name);
        Assert.Equal("GREAT TROLL", Data.GetMonster(69).Name);
    }

    [OriginalDataFact]
    public void ClassSeven_FightsOncePerTrip_ThenPlainMove_NeverRolls()
    {
        var t = Data.CreateTraversal();
        var from = FindStepEast(t, (r, x, y) => t.GetRegion(r).GetNormalized(x, y) == 75
                                                && t.GetRegion(r).GetNormalized(x - 1, y) != 75);
        var rng = new Scripted(0); // 0 would be a random encounter on ordinary terrain

        var state = At(from);
        var first = t.TryMove(state, CardinalDirection.East, rng);
        Assert.Equal(OverworldMoveKind.TileClassFight, first.Kind);
        Assert.Equal(69, first.MonsterId);
        Assert.True(state.TileClassFightDone);

        var again = At(from);
        again.TileClassFightDone = true;
        var second = t.TryMove(again, CardinalDirection.East, rng);
        Assert.Equal(OverworldMoveKind.Moved, second.Kind);
        Assert.Null(second.MonsterId);
        Assert.Equal(0, rng.Draws); // no 6 % roll on class 7, either way
    }

    [OriginalDataFact]
    public void Session_TileFlag_ClearedByLeavingTownAndDungeon()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.Party.Members.Add(Member("a"));
        session.Roster.Slots[0] = session.Party.Members[0];
        session.DepartGuildToWilderness();
        session.Overworld.TileClassFightDone = true;
        session.EnterTown(1);
        Assert.True(session.LeaveTown());
        Assert.False(session.Overworld.TileClassFightDone);

        session.Overworld.TileClassFightDone = true;
        session.EnterDungeon(1, 20, 0);
        session.ExitDungeonToWilderness();
        Assert.False(session.Overworld.TileClassFightDone);
    }

    // --- Fixed encounters (x7) ---------------------------------------------------------------------

    [OriginalDataFact]
    public void FixedEncounter_UsesArg_SpendsTheTile()
    {
        var t = Data.CreateTraversal();
        var from = FindStepEast(t, (r, x, y) => r == 16 && x == 6 && y == 11); // OUT16 (6,11): tile 7, arg 54
        var state = At(from);
        var res = t.TryMove(state, CardinalDirection.East, new Scripted(0));
        Assert.Equal(OverworldMoveKind.ScriptedEncounter, res.Kind);
        Assert.Equal(54, res.MonsterId);         // ORANGE DRAGON; DOS [0x64e6] = 55
        Assert.Equal(55, res.FeatureArg);        // unchanged API: arg + 1
        Assert.Equal(8, t.GetRegion(16).GetNormalized(6, 11)); // inc byte [cell]: x7 → x8

        var back = At(from);
        var rng = new Scripted(0);
        var again = t.TryMove(back, CardinalDirection.East, rng);
        Assert.Equal(OverworldMoveKind.Moved, again.Kind);
        Assert.Equal(0, rng.Draws);
    }

    [OriginalDataFact]
    public void FixedEncounter_InSession_OpensWithStaleGlobals()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.Party.Members.Add(Member("a"));
        session.DepartGuildToWilderness();
        var from = FindStepEast(Data.CreateTraversal(), (r, x, y) => r == 16 && x == 6 && y == 11);
        (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y) = from;
        session.SurpriseGlobals = SurpriseFlags.None; // what a fight that ran a full round leaves

        session.MoveOverworld(CardinalDirection.East);
        Assert.Equal(GameMode.Combat, session.Mode);
        Assert.Equal(54, session.Combat!.FirstMonsterId);
        Assert.Equal(SurpriseFlags.None, session.Combat.Surprise);
        Assert.False(session.CombatRoundHasOptions);
    }

    // --- Day branches (0x723b) ---------------------------------------------------------------------

    [Fact]
    public void BlackKnights_FleeFromHighScore_NoFlagWrite()
    {
        var g = SurpriseFlags.Normal;
        var o = WildernessEncounter.RollDay(PartyOf(Member("a")), 84, 0, 0, g, new Scripted(92));
        Assert.Null(o.MonsterId);
        Assert.Null(o.Prompt);
        Assert.Equal(g, o.Globals);
        Assert.Equal(WildernessEncounter.BlackKnightsFlee, Assert.Single(o.Messages));
    }

    [OriginalDataFact]
    public void BlackKnights_Menu_Answers()
    {
        var o = WildernessEncounter.RollDay(PartyOf(Member("a")), 83, 0, 0, SurpriseFlags.Normal, new Scripted(92));
        var p = Assert.IsType<WildernessPrompt>(o.Prompt);
        Assert.Equal(WildernessMenu.BlackKnights, p.Menu);
        Assert.Equal(76, p.MonsterId);
        Assert.Equal("BLACK KNIGHT", Data.GetMonster(76).Name);

        var stale = new SurpriseFlags(true, 2);
        var away = WildernessEncounter.Answer(p, WildernessAnswer.Run, stale, new Scripted(43));
        Assert.Null(away.MonsterId);
        Assert.Equal(new SurpriseFlags(false, 2), away.Globals); // 0x7284: F := 0 even without a fight

        var caught = WildernessEncounter.Answer(p, WildernessAnswer.Run, stale, new Scripted(44));
        Assert.Equal((76, SurpriseFlags.Normal), (caught.MonsterId!.Value, caught.Globals));

        var fight = WildernessEncounter.Answer(p, WildernessAnswer.Fight, stale, new Scripted(0));
        Assert.Equal((76, SurpriseFlags.Stalled), (fight.MonsterId!.Value, fight.Globals));

        var surrender = WildernessEncounter.Answer(p, WildernessAnswer.Surrender, stale, new Scripted(0));
        Assert.Null(surrender.MonsterId);
        Assert.True(surrender.SurrenderFoundGold);
        Assert.Throws<ArgumentException>(() => WildernessEncounter.Answer(p, WildernessAnswer.Attack, stale, new Scripted(0)));
    }

    [Fact]
    public void SleepingMonsters_TenthPick_ThenAttackOrLeave()
    {
        // 50: no Black Knights; 10 < 25: pick rand/10 + 12T + 2; 37 → 3; 20 < 31: sleeping menu.
        var o = WildernessEncounter.RollDay(PartyOf(Member("a")), 0, 3, 1, SurpriseFlags.None, new Scripted(50, 10, 37, 20));
        var p = Assert.IsType<WildernessPrompt>(o.Prompt);
        Assert.Equal(WildernessMenu.SleepingMonsters, p.Menu);
        Assert.Equal(3 + 12 + 2, p.MonsterId);

        var attack = WildernessEncounter.Answer(p, WildernessAnswer.Attack, SurpriseFlags.None, new Scripted(0));
        Assert.Equal(SurpriseFlags.MonstersSurprised, attack.Globals);
        Assert.Equal(17, attack.MonsterId);
        Assert.Null(WildernessEncounter.Answer(p, WildernessAnswer.Leave, SurpriseFlags.None, new Scripted(0)).MonsterId);
    }

    [Fact]
    public void PairPick_UsesTable_NobodyHears_NormalFight()
    {
        // 50; 30 ≥ 25 → pair pick: coin 60·2/100 = 1, index 75/25 + class 0 = 3 → pair 3 → 1 + 6 + 12·0 = 7 (COYOTE).
        // 99: no sleeping menu; listener 99·222/100 = 219 > listen; 99 ≥ 6·0 + 20 → normal fight.
        var rng = new Scripted(50, 30, 60, 75, 99, 99, 99);
        var o = WildernessEncounter.RollDay(PartyOf(Member("a", listen: 50)), 0, 0, 0, SurpriseFlags.None, rng);
        Assert.Equal(7, o.MonsterId);
        Assert.Equal(SurpriseFlags.Normal, o.Globals);
        Assert.Equal(7, rng.Draws);
    }

    [OriginalDataFact]
    public void Listener_DeadMemberStillDraws_LastSuccessHears()
    {
        var party = PartyOf(Member("alive", listen: 50), Member("dead", CharacterStatus.Dead, listen: 99));
        // pick 50, 30, 0, 0 → monster 2·4 + 0 = 8 (ORC); 99 no sleep; listener rolls 10 (22 ≤ 50), 0 (dead: ignored);
        // 5 < 20 → heard and fight at once.
        var rng = new Scripted(50, 30, 0, 0, 99, 10, 0, 5);
        var o = WildernessEncounter.RollDay(party, 0, 0, 0, SurpriseFlags.None, rng);
        Assert.Equal(8, o.MonsterId);
        Assert.Equal(SurpriseFlags.Normal, o.Globals);
        Assert.Equal("alive HEARS MONSTERS", Assert.Single(o.Messages));
        Assert.Equal(8, rng.Draws);
    }

    [Fact]
    public void HearsMonsters_Menu_Answers()
    {
        var rng = new Scripted(50, 30, 0, 0, 99, 10, 20);
        var o = WildernessEncounter.RollDay(PartyOf(Member("a", listen: 50)), 0, 0, 0, SurpriseFlags.None, rng);
        var p = Assert.IsType<WildernessPrompt>(o.Prompt);
        Assert.Equal((WildernessMenu.HearsMonsters, 0), (p.Menu, p.ListenerSlot!.Value));

        Assert.Equal(SurpriseFlags.Normal, WildernessEncounter.Answer(p, WildernessAnswer.Fight, default, new Scripted(29)).Globals);
        var surprise = WildernessEncounter.Answer(p, WildernessAnswer.Fight, default, new Scripted(30));
        Assert.Equal(SurpriseFlags.MonstersSurprised, surprise.Globals);
        Assert.Equal(WildernessEncounter.YouSurpriseThem, Assert.Single(surprise.Messages));
        Assert.Null(WildernessEncounter.Answer(p, WildernessAnswer.Run, default, new Scripted(79)).MonsterId);
        Assert.Equal(8, WildernessEncounter.Answer(p, WildernessAnswer.Run, default, new Scripted(80)).MonsterId);
    }

    [Fact]
    public void NobodyHears_MonstersSurprise_OrFallOffQuirk()
    {
        var party = PartyOf(Member("a"));
        var surprised = WildernessEncounter.RollDay(party, 0, 0, 0, default, new Scripted(50, 30, 0, 0, 99, 99, 10, 50));
        Assert.Equal(SurpriseFlags.PartySurprised, surprised.Globals);
        Assert.Equal(8, surprised.MonsterId);

        var stale = new SurpriseFlags(true, 1);
        var fallOff = WildernessEncounter.RollDay(party, 0, 0, 0, stale, new Scripted(50, 30, 0, 0, 99, 99, 10, 30));
        Assert.Equal(29, fallOff.MonsterId);  // AX = 30 taken as a 1-based id, whatever the terrain
        Assert.Equal(stale, fallOff.Globals); // no flag write

        var none = WildernessEncounter.RollDay(party, 0, 0, 0, stale, new Scripted(50, 30, 0, 0, 99, 99, 10, 0));
        Assert.Null(none.MonsterId);
    }

    [OriginalDataFact]
    public void PairTable_LoadsFromJson()
    {
        Assert.Equal(WildernessEncounterRules.Dos.PairTable, Data.WildernessEncounters.PairTable);
    }

    // --- Session wiring + persistence ----------------------------------------------------------------

    [OriginalDataFact]
    public void Session_SurrenderZeroesFoundGold_HighestScoreFromTownExit()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.Party.Members.Add(Member("a", score: 10));
        session.Party.Members.Add(Member("b", CharacterStatus.Dead, score: 90));
        session.Roster.Slots[0] = session.Party.Members[0];
        session.Roster.Slots[1] = session.Party.Members[1];
        session.EnterTown(1);
        session.LeaveTown();
        Assert.Equal(90, session.HighestScore); // DEAD slots count (OV1 0x3283 loops every party slot)

        session.HighestScore = 0;
        session.Sack.Gold = 500;
        session.Party.GoldCarried = 301;
        // A pending menu, as a save made at the prompt would hold it.
        var snap = session.CaptureSnapshot();
        snap.Wilderness!.PromptMenu = WildernessMenu.BlackKnights;
        snap.Wilderness.PromptMonsterId = 76;
        snap.Wilderness.PromptTitle = WildernessEncounter.BlackKnightsTitle;
        session.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));
        Assert.Equal(WildernessMenu.BlackKnights, session.WildernessPrompt!.Menu);
        Assert.Throws<InvalidOperationException>(() => session.MoveOverworld(CardinalDirection.East));

        Assert.Null(session.AnswerWildernessPrompt(WildernessAnswer.Surrender));
        Assert.Equal(0, session.Sack.Gold);
        Assert.Equal(0, session.Party.GoldCarried); // one purse in DOS (s49 DOSBox)
        Assert.Null(session.WildernessPrompt);
        Assert.False(session.SurpriseGlobals.FirstRound);
    }

    [OriginalDataFact]
    public void Snapshot_RoundTripsWildernessState_AndRegionBytes()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.Party.Members.Add(Member("a"));
        session.Roster.Slots[0] = session.Party.Members[0];
        session.DepartGuildToWilderness();
        session.Overworld.TileClassFightDone = true;
        session.SurpriseGlobals = new SurpriseFlags(false, 2);
        session.HighestScore = 42;
        var from = FindStepEast(Data.CreateTraversal(), (r, x, y) => r == 16 && x == 6 && y == 11);
        (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y) = from;
        session.MoveOverworld(CardinalDirection.East); // spends OUT16 (6,11) and starts the dragon fight (saved mid-combat)

        var json = SessionCodec.WriteJson(session.CaptureSnapshot());
        var restored = GameSessionFactory.Create(Data, new Scripted(99));
        restored.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.True(restored.Overworld.TileClassFightDone);
        Assert.Equal(42, restored.HighestScore);
        Assert.Equal(new SurpriseFlags(false, 2), restored.SurpriseGlobals);
        var snap = restored.CaptureSnapshot();
        Assert.Equal(8, snap.Wilderness!.RegionTiles![16][11 * OverworldRegion.Width + 6] % OverworldRegion.FogOffset);
    }

    [OriginalDataFact]
    public void Snapshot_OlderSaveWithoutWilderness_LoadsDefaults()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(99));
        session.Party.Members.Add(Member("a", score: 12));
        session.Roster.Slots[0] = session.Party.Members[0];
        var node = JsonNode.Parse(SessionCodec.WriteJson(session.CaptureSnapshot()))!.AsObject();
        Assert.True(node.Remove("wilderness"));

        var restored = GameSessionFactory.Create(Data, new Scripted(99));
        restored.Overworld.TileClassFightDone = true;
        restored.SurpriseGlobals = SurpriseFlags.Normal;
        restored.ApplySnapshot(SessionCodec.ReadJson(node.ToJsonString()));
        Assert.False(restored.Overworld.TileClassFightDone);
        Assert.Equal(SurpriseFlags.None, restored.SurpriseGlobals);
        Assert.Null(restored.WildernessPrompt);
    }

    // --- Region edges (0x61ce) ---------------------------------------------------------------------

    [OriginalDataFact]
    public void CrossingOntoAWall_LeavesThePartyOnTheWallCell_NoTime()
    {
        var t = Data.CreateTraversal();
        Assert.Equal(OverworldRegion.WallTile, t.GetRegion(12).GetNormalized(9, 19));
        var state = new OverworldState { RegionId = 13, X = 9, Y = 0 };
        var res = t.TryMove(state, CardinalDirection.North); // OUT13 → 13 − 1 = OUT12 (the "asymmetry")
        Assert.Equal(OverworldMoveKind.Moved, res.Kind);
        Assert.Equal((12, 9, 19), (state.RegionId, state.X, state.Y));
        Assert.Equal(0, res.TimeCost);
    }

    [OriginalDataFact]
    public void CrossingToAMissingRegion_IsBlocked()
    {
        var t = Data.CreateTraversal();
        var state = new OverworldState { RegionId = 1, X = 0, Y = 0 };
        var res = t.TryMove(state, CardinalDirection.North); // region 0: no OUT0.DAT ([?] in DOS)
        Assert.Equal(OverworldMoveKind.Blocked, res.Kind);
        Assert.Equal((1, 0, 0), (state.RegionId, state.X, state.Y));
    }

    // --- Session integration -----------------------------------------------------------------------

    [OriginalDataFact]
    public void MoveOverworld_RandomDayEncounter_BlackKnightsPrompt_BlocksMoves()
    {
        var t = Data.CreateTraversal();
        var from = FindStepEast(t, (r, x, y) =>
        {
            var tile = t.GetRegion(r).GetNormalized(x, y);
            int c = tile / 10, sub = tile % 10;
            return tile is not (100 or 40 or 41) && c is not (5 or 7 or 10 or 11) && sub is not (6 or 7 or 8);
        });
        // 0: the 6 % step roll; 35: not a night encounter; 92: Black Knights (score 0 ≤ 83 → menu).
        var session = GameSessionFactory.Create(Data, new Scripted(0, 35, 92, 0));
        session.Party.Members.Add(Member("a"));
        session.DepartGuildToWilderness();
        (session.Overworld.RegionId, session.Overworld.X, session.Overworld.Y) = from;

        var res = session.MoveOverworld(CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.RandomEncounter, res.Kind);
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Equal(WildernessMenu.BlackKnights, session.WildernessPrompt!.Menu);
        Assert.Throws<InvalidOperationException>(() => session.MoveOverworld(CardinalDirection.East));

        var fight = session.AnswerWildernessPrompt(WildernessAnswer.Fight);
        Assert.NotNull(fight);
        Assert.Equal(76, fight!.FirstMonsterId);
        Assert.Equal(SurpriseFlags.Stalled, fight.Surprise);
        Assert.Equal(GameMode.Combat, session.Mode);
    }

    [OriginalDataFact]
    public void FightEnd_CopiesLiveFlagsBackToTheGlobals()
    {
        var session = GameSessionFactory.Create(Data, new Scripted(0));
        session.Party.Members.Add(Member("a"));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(0, SurpriseFlags.Normal);
        Assert.Equal(SurpriseFlags.Normal, session.SurpriseGlobals);
        session.Combat!.Surprise = new SurpriseFlags(true, 2); // stand-in for whatever the fight left

        session.ChoosePartyOption(PartyOption.Flee); // ends at PARTY OPTIONS (monsters run or the flee works)
        Assert.Null(session.Combat);
        Assert.Equal(new SurpriseFlags(true, 2), session.SurpriseGlobals);
    }
}
