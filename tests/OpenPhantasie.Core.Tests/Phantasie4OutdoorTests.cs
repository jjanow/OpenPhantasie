using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's outdoor and town loops (T6.3, <see cref="P4OutdoorLoop"/>, <see cref="P4TownLoop"/>; specs
/// <c>outdoor.md</c>, <c>towns.md</c>): the map grid, blocked moves, the arrival step, ships, encounters and their surprise,
/// the town entry, the inn, the shipwright and leaving town.
/// </summary>
public class Phantasie4OutdoorTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();

    /// <summary>Returns the scripted rand results (1..n) in order, then n (the highest) for every later draw.</summary>
    private sealed class ScriptRng(params int[] results) : IRandomSource
    {
        private readonly Queue<int> _q = new(results);

        public int Draws { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            return _q.Count > 0 ? Math.Clamp(_q.Dequeue(), minInclusive, maxExclusive - 1) : maxExclusive - 1;
        }
    }

    private static P4Party Party(int members = 3)
    {
        var p = new P4Party { Gold = 5000 };
        for (var i = 0; i < members; i++)
        {
            var m = new P4Member { Name = $"M{i}", Level = 3, HpMax = 30, Hp = 30, Defence = 10, Abilities = [10, 10, 10, 10, 10, 10],
                AgingFactor = 100 };
            Array.Fill(m.Skills, 50);
            p.Members.Add(m);
        }
        return p;
    }

    private static P4OutdoorLoop Loop(P4Party party, IRandomSource rng, GameOptions? o = null) =>
        new(o ?? Manual, T, Atlas, party, rng);

    private static int TypeAt(int map, int x, int y) => P4OutdoorMap.TypeOf(Atlas.Outdoors[map].Tile(x, y));

    // --- Map grid -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(7, 25, 10, 4, 8, 0, 10)]   // E off the right edge: the next map, x = 0
    [InlineData(7, 0, 10, 3, 6, 25, 10)]   // W
    [InlineData(7, 10, 0, 6, 2, 10, 25)]   // N: 5 maps back, y = 25
    [InlineData(7, 10, 25, 1, 12, 10, 0)]  // S
    [InlineData(7, 25, 10, 7, 8, 0, 9)]    // NE leaving only east: y still changes on the new map
    [InlineData(7, 25, 0, 7, 3, 0, 25)]    // NE through the corner
    [InlineData(17, 10, 0, 6, 14, 10, 25)] // the Netherworld grid is 3 wide
    [InlineData(7, 12, 12, 6, 7, 12, 11)]  // inside the map
    public void Step_FollowsTheMapGrid(int map, int x, int y, int dir, int toMap, int toX, int toY) =>
        Assert.Equal((toMap, toX, toY), P4OutdoorLoop.Step(map, x, y, dir));

    [OriginalDataFact]
    public void NoPassableBorderSquare_LeadsOffTheGrid()
    {
        var bad = new List<string>();
        foreach (var (n, m) in Atlas.Outdoors)
            for (var y = 0; y < P4OutdoorMap.Size; y++)
                for (var x = 0; x < P4OutdoorMap.Size; x++)
                {
                    if (x is > 0 and < 25 && y is > 0 and < 25)
                        continue;
                    var type = P4OutdoorMap.TypeOf(m.Tile(x, y));
                    if (T.Outdoor.BlockedTypes.Contains(type))
                        continue; // no step reaches it from inside the map
                    for (var d = 0; d < 8; d++)
                    {
                        var (to, _, _) = P4OutdoorLoop.Step(n, x, y, d);
                        if (to != n && !Atlas.Outdoors.ContainsKey(to))
                            bad.Add($"map {n} ({x},{y}) dir {d} → {to}");
                    }
                }
        Assert.Empty(bad);
    }

    // --- Moving -------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void ABlockedMove_CostsNothing()
    {
        // Map 1 is ringed by open sea (type 19); (1, 1) is water inside it.
        Assert.Equal(19, TypeAt(1, 0, 1));
        var party = Party();
        party.Ship = P4ShipState.Aboard; // no swimming
        party.ShipMap = 1;
        var rng = new ScriptRng(1, 99); // the arrival step: the navigator keeps the course; no encounter
        var o = Loop(party, rng);
        o.Arrive(1, 1, 1);
        Assert.Equal(P4OutdoorPrompt.Action, o.Prompt);
        var (days, hours, draws) = (party.DaysOut, party.Daytime, rng.Draws);
        o.Move(3); // west into the open sea
        Assert.Equal((1, 1), (o.X, o.Y));
        Assert.Equal((days, hours, draws), (party.DaysOut, party.Daytime, rng.Draws));
        Assert.Contains(o.Box, l => l.Contains(GameText.S("p4x.0x43123").Trim()));
    }

    [OriginalDataFact]
    public void EveryArrival_RunsTheSquaresStep_HoursIncluded()
    {
        var party = Party();
        var o = Loop(party, new ScriptRng(1000)); // no encounter
        Assert.Equal(6, TypeAt(9, 9, 10));       // Pendragon's exit: grassland, 6 hours
        o.Arrive(9, 9, 10);
        Assert.Equal((0, 6), (party.DaysOut, party.Daytime));
        Assert.Contains(o.Box, l => l == GameText.S("p4x.0x43021"));
    }

    [OriginalDataFact]
    public void TheMapToggle_ByTheManual_OnlySwitchesTheView()
    {
        var party = Party();
        var o = Loop(party, new ScriptRng(1000));
        o.Arrive(9, 9, 10);
        o.ToggleMap();
        Assert.True(o.MapOverview);
        Assert.Equal(6, party.Daytime);
        Assert.Equal(P4OutdoorPrompt.Action, o.Prompt);
        o.ToggleMap();
        Assert.False(o.MapOverview);
        Assert.Equal(6, party.Daytime);
    }

    [OriginalDataFact]
    public void TheMapToggle_X68000_RunsTheStepAgain()
    {
        var party = Party();
        var o = Loop(party, new ScriptRng(1000, 1000), Code);
        o.Arrive(9, 9, 10);
        o.ToggleMap();
        Assert.True(o.MapOverview);
        Assert.Equal(12, party.Daytime);
    }

    [OriginalDataFact]
    public void Overview_Centred_TheNineMapsAroundTheParty_BlackPastTheEdge()
    {
        var t = T.Outdoor.Overview;
        // Scandor is 5 × 3 (blocks 0 and 1 overlap); the Netherworld 3 × 3 is a world of its own.
        Assert.Equal([3, 4, 5, 8, 9, 10, 13, 14, 15], t.CentredOn(9));
        Assert.Equal([-1, -1, -1, -1, 1, 2, -1, 6, 7], t.CentredOn(1));
        Assert.Equal([9, 10, -1, 14, 15, -1, -1, -1, -1], t.CentredOn(15));
        Assert.Equal([-1, -1, -1, 16, 17, 18, 19, 20, 21], t.CentredOn(17));

        var options = GameOptions.ForPreset(GameId.P4).With("p4.overview", "centred");
        var o = Loop(Party(), new ScriptRng(1000), options);
        o.Arrive(9, 9, 10); // Pendragon's exit: the same nine maps as block 1
        Assert.Equal([3, 4, 5, 8, 9, 10, 13, 14, 15], o.OverviewMaps);
        Assert.Equal(4, o.OverviewCell);
        Assert.Equal(-1, o.OverviewCellOf(-1));
    }

    [OriginalDataTheory]
    [InlineData(3, -1, false, 1)] // first arrival: the map's own block (_mvb)
    [InlineData(3, 0, false, 0)]  // walking east from map 2 into map 3: block 0 still holds it
    [InlineData(3, 0, true, 1)]   // a jump to map 3: _mvb
    [InlineData(4, 0, false, 1)]  // off block 0's outer column: block 1
    [InlineData(7, 1, false, 0)]  // back west off block 1's outer column: block 0
    [InlineData(17, 1, true, 2)]  // the Netherworld
    public void Overview_Block_StaysOnAWalk_TheMapsOwnOnAJump(int map, int current, bool jump, int block) =>
        Assert.Equal(block, T.Outdoor.Overview.BlockFor(map, current, jump));

    [OriginalDataFact]
    public void Overview_Maps_TheBlock_TheNetherworldShowsOnlyTheMapItself()
    {
        var o = Loop(Party(), new ScriptRng(1000));
        o.Arrive(9, 9, 10); // Pendragon's exit
        Assert.Equal(1, o.OverviewBlock);
        Assert.Equal([3, 4, 5, 8, 9, 10, 13, 14, 15], o.OverviewMaps);
        Assert.Equal(4, o.OverviewCell);

        // A Netherworld square the party can stand on (ground, types 11 / 12).
        var (x, y) = Enumerable.Range(0, P4OutdoorMap.Size * P4OutdoorMap.Size)
            .Select(i => (i % P4OutdoorMap.Size, i / P4OutdoorMap.Size)).First(p => TypeAt(20, p.Item1, p.Item2) is 11 or 12);
        var n = Loop(Party(), new ScriptRng(1000));
        n.Arrive(20, x, y);
        Assert.Equal(2, n.OverviewBlock);
        Assert.Equal([16, 16, 16, 16, 20, 16, 16, 16, 16], n.OverviewMaps);
        Assert.Equal(4, n.OverviewCell);
        var copy = Loop(Party(), new ScriptRng(1000));
        copy.Restore(n.Capture());
        Assert.Equal(2, copy.OverviewBlock);
    }

    // --- Encounters ---------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void SleepingMonsters_Attacked_GiveTheParty_TheFreeRound_WithoutAWait()
    {
        var o = Loop(Party(), new ScriptRng(1, 50, 1));
        o.Arrive(9, 9, 10);
        Assert.Equal(P4OutdoorPrompt.Confirm, o.Prompt);
        o.Answer(true);
        Assert.Equal(P4OutdoorPrompt.Fight, o.Prompt);
        Assert.Equal(new P4OutdoorFight(true, false, false, false, 0), o.PendingFight);
    }

    [OriginalDataFact]
    public void SleepingMonsters_LeftAlone_NoFight()
    {
        var o = Loop(Party(), new ScriptRng(1, 50, 1));
        o.Arrive(9, 9, 10);
        o.Answer(false);
        Assert.Equal(P4OutdoorPrompt.Action, o.Prompt);
        Assert.Null(o.PendingFight);
    }

    [OriginalDataFact]
    public void NobodySenses_AndRand10AtLeast5_TheMonstersSurpriseTheParty_AfterAKey()
    {
        // rand(1000) 1: encounter; 50: no night attack; 50: no sleepers; rand(188) 188 × 3: nobody senses; rand(100) 1 <
        // 8 × 0 + 30: they come on; rand(10) 5: surprise.
        var o = Loop(Party(), new ScriptRng(1, 50, 50, 188, 188, 188, 1, 5));
        o.Arrive(9, 9, 10);
        Assert.Equal(P4OutdoorPrompt.Wait, o.Prompt);
        o.Continue();
        Assert.Equal(new P4OutdoorFight(false, true, false, false, 0), o.PendingFight);
    }

    [OriginalDataTheory]
    [InlineData(2, false, true)] // rand(10) < 3: they surprise the party after all
    [InlineData(3, true, false)] // else the party surprises them
    public void SomeoneSenses_Fight_Yes(int roll, bool partySurprises, bool monstersSurprise)
    {
        // Member 1 senses (rand(188) 1 ≤ 50); rand(100) 99 ≥ 30: "Fight?".
        var o = Loop(Party(), new ScriptRng(1, 50, 50, 188, 1, 188, 99, roll));
        o.Arrive(9, 9, 10);
        Assert.Equal(P4OutdoorPrompt.Confirm, o.Prompt);
        o.Answer(true);
        o.Continue();
        Assert.Equal(new P4OutdoorFight(partySurprises, monstersSurprise, false, false, 0), o.PendingFight);
    }

    [OriginalDataTheory]
    [InlineData(7, false)] // rand(10) < 8: avoided
    [InlineData(8, true)]  // else they approach
    public void SomeoneSenses_Fight_No(int roll, bool fight)
    {
        var o = Loop(Party(), new ScriptRng(1, 50, 50, 188, 1, 188, 99, roll));
        o.Arrive(9, 9, 10);
        o.Answer(false);
        Assert.Equal(fight ? P4OutdoorPrompt.Wait : P4OutdoorPrompt.Action, o.Prompt);
    }

    [OriginalDataTheory]
    [InlineData(false, P4Member.Asleep)] // code: the sleepers stay asleep
    [InlineData(true, P4Member.Okay)]    // D4-048 manual: the watch wakes them
    public void NightAttack_TheWatchHears_NoSurprise(bool manual, int othersAfter)
    {
        // rand(1000) 1; rand(100) 1 < 38: night; watch rand(3) 1 → member 0; rand(100) 1 < Listen 50: heard.
        var party = Party();
        var o = Loop(party, new ScriptRng(1, 1, 1, 1), manual ? Manual : Code);
        o.Arrive(9, 9, 10);
        o.Continue();
        Assert.Equal(new P4OutdoorFight(false, false, false, true, 0), o.PendingFight);
        Assert.Equal(P4Member.Okay, party.Members[0].Condition);
        Assert.All(party.Members.Skip(1), m => Assert.Equal(othersAfter, m.Condition));
    }

    [OriginalDataFact]
    public void AfterAFight_ThePartyIsReloadedOnTheSquare_AndItsStepRunsAgain()
    {
        var party = Party();
        party.Levitating = true;
        var o = Loop(party, new ScriptRng(1, 50, 1));
        o.Arrive(9, 9, 10);
        o.Answer(true);
        Assert.Equal(6, party.Daytime);
        o.FightOver(P4FightOutcome.Won);
        Assert.Equal(12, party.Daytime); // the grassland's 6 hours again
        Assert.False(party.Levitating);  // every fight ends Levitation
        Assert.Equal(P4OutdoorPrompt.Action, o.Prompt);
    }

    [OriginalDataFact]
    public void ALostFight_EndsTheGame()
    {
        var o = Loop(Party(), new ScriptRng(1, 50, 1));
        o.Arrive(9, 9, 10);
        o.Answer(true);
        o.FightOver(P4FightOutcome.Lost);
        Assert.Equal(P4OutdoorPrompt.PartyLost, o.Prompt);
        // s162: _deadmsg's two lines, as in a location map.
        Assert.Equal([GameText.S("p4x.0x44097"), GameText.S("p4x.0x440b0")], o.Box.TakeLast(2));
    }

    // --- Ships --------------------------------------------------------------------------------------------------------

    private static P4Party Aboard(int map, int x, int y)
    {
        var p = Party();
        (p.Ship, p.ShipMap, p.ShipX, p.ShipY) = (P4ShipState.Aboard, map, x, y);
        return p;
    }

    [OriginalDataFact]
    public void AForcedMoveOntoLand_WithMovesLeft_RunsAground()
    {
        // Pendleton's ship square (8: 17, 5) is water; (18, 6) to the SE is hills.
        Assert.Equal((0, 3), (TypeAt(8, 17, 5), TypeAt(8, 18, 6)));
        var party = Aboard(8, 17, 5);
        // Drift: rand(100) 100 > Navigation 50; rand(100) 1 < 15: a storm (10 moves); no encounter; rand(8) 3: SE.
        var o = Loop(party, new ScriptRng(100, 1, 1000, 3));
        o.Arrive(8, 17, 5);
        Assert.Equal(P4OutdoorPrompt.Wait, o.Prompt);
        Assert.Equal((18, 6), (o.X, o.Y));
        Assert.Equal(P4ShipState.None, party.Ship);
        Assert.Equal(0, o.Drift);
        Assert.Contains(o.Box, l => l.Contains(GameText.S("p4x.0x4315f").Trim()));
    }

    [OriginalDataFact]
    public void TheLastForcedMoveOntoLand_LeavesTheShip_MooredBehind()
    {
        var party = Aboard(8, 17, 5);
        // rand(100) 100 > 50: drift; rand(100) 50 ≥ 15: one move; no encounter; SE.
        var o = Loop(party, new ScriptRng(100, 50, 1000, 3));
        o.Arrive(8, 17, 5);
        Assert.Equal(P4OutdoorPrompt.Wait, o.Prompt);
        Assert.Equal((P4ShipState.Moored, 8, 17, 5), (party.Ship, party.ShipMap, party.ShipX, party.ShipY));
        Assert.Contains(o.Box, l => l.Contains(GameText.S("p4x.0x4316e").Trim()));
    }

    [OriginalDataFact]
    public void SteppingOnTheMooredShip_Boards_TheBestNavigatorSteers()
    {
        var party = Party();
        party.Members[2].Skills[(int)P4Skill.Navigation] = 80;
        (party.Ship, party.ShipMap, party.ShipX, party.ShipY) = (P4ShipState.Moored, 8, 17, 5);
        var o = Loop(party, new ScriptRng(1000, 1000));
        o.Arrive(8, 18, 6);
        o.Move(5); // NW onto the ship
        Assert.Equal(P4OutdoorPrompt.Wait, o.Prompt);
        Assert.Equal((P4ShipState.Aboard, 2), (party.Ship, party.ShipNavigator));
        Assert.Equal(0, party.ShipSeaSteps); // the step onto the ship was not sailed
    }

    [OriginalDataFact]
    public void Swimming_HurtsMembersWhoFailTheirSwimRoll()
    {
        var party = Party();
        // Member 0 rand(100) 51 > 50: rand(5) 2 + 1 = 3; member 1 50: fine; member 2 asleep: always hurt, rand(5) 1 + 1.
        party.Members[2].Condition = P4Member.Asleep;
        var o = Loop(party, new ScriptRng(51, 2, 50, 1, 1000), Code);
        o.Arrive(8, 17, 5);
        Assert.Equal((27, 30, 28), (party.Members[0].Hp, party.Members[1].Hp, party.Members[2].Hp));
    }

    [OriginalDataFact]
    public void Levitating_ThePartyDoesNotSwim_UnderTheManualOnly()
    {
        var party = Party();
        party.Levitating = true;
        Loop(party, new ScriptRng(100, 100, 100, 100, 100, 100, 1000)).Arrive(8, 17, 5);
        Assert.All(party.Members, m => Assert.Equal(30, m.Hp));
        var code = Party();
        code.Levitating = true;
        Loop(code, new ScriptRng(100, 1, 100, 1, 100, 1, 1000), Code).Arrive(8, 17, 5);
        Assert.All(code.Members, m => Assert.Equal(28, m.Hp));
    }

    // --- Places -------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void ATownSquare_AsksAndLeavesForTheTown()
    {
        var o = Loop(Party(), new ScriptRng());
        o.Arrive(8, 16, 6); // Pendleton's block
        Assert.Equal(P4OutdoorPrompt.Confirm, o.Prompt);
        o.Answer(true);
        Assert.Equal(P4OutdoorPrompt.Left, o.Prompt);
        Assert.Equal(new P4Transfer(P4Transfer.Town, 2, 0, 0, 0), o.Transfer);
    }

    [OriginalDataFact]
    public void ADungeonSquare_LeadsToItsEntry()
    {
        var o = Loop(Party(), new ScriptRng());
        o.Arrive(1, 16, 2); // Temple of the Duck
        o.Answer(true);
        Assert.Equal(new P4Transfer(P4Transfer.Dungeon, 17, 15, 29, 0), o.Transfer);
    }

    [OriginalDataFact]
    public void TheOutdoorInn_Costs256_And1000Hours_ThenAsksAgain()
    {
        var party = Party();
        party.Members[0].Hp = 1;
        var o = Loop(party, new ScriptRng());
        o.Arrive(1, 4, 13); // the Skull inn
        Assert.Contains(o.Box, l => l.Contains("Skull"));
        o.Answer(true);
        var (days, hour) = (party.DaysOut, party.Daytime);
        o.Continue(); // _oinn's own wait ("paid 256 gold"), then _inn
        Assert.Equal(5000 - 256, party.Gold);
        Assert.Equal(30, party.Members[0].Hp);
        // s165: _inn's window with the sleepers' rows and its own SPACE wait; _dayout(1000) only after it.
        Assert.Equal(P4OutdoorPrompt.Wait, o.Prompt);
        Assert.Equal([0, 1, 2], o.InnRows!.Select(r => r.Member));
        Assert.Equal((days, hour), (party.DaysOut, party.Daytime));
        o.Continue();
        Assert.Null(o.InnRows);
        Assert.Equal((42, 8), (party.DaysOut, party.Daytime)); // 8 + 1000 + 8 hours (arrival, stay, reload)
        Assert.Equal(P4OutdoorPrompt.Confirm, o.Prompt);
    }

    [OriginalDataFact]
    public void TheOutdoorInn_ASaveAtItsSecondWait_KeepsTheRows()
    {
        var party = Party();
        var o = Loop(party, new ScriptRng());
        o.Arrive(1, 4, 13);
        o.Answer(true);
        o.Continue();
        var snap = o.Capture();
        var copy = Loop(party, new ScriptRng());
        copy.Restore(snap);
        Assert.Equal(P4OutdoorPrompt.Wait, copy.Prompt);
        Assert.Equal(o.InnRows, copy.InnRows);
        copy.Continue();
        Assert.Null(copy.InnRows);
        Assert.Equal(P4OutdoorPrompt.Confirm, copy.Prompt);
    }

    [OriginalDataFact]
    public void ThePool_OnMap1_Teleports()
    {
        var o = Loop(Party(), new ScriptRng(1000, 1000));
        o.Arrive(1, 19, 21);
        o.Answer(true);
        Assert.Equal((23, 20, 8), (o.Map, o.X, o.Y));
    }

    [OriginalDataFact]
    public void TheRiverOfHell_NeedsBothOfCharonsItems()
    {
        Assert.False(T.Outdoor.CharonItems.All(Party().HasItem));
        var p = Party();
        p.Pack[0] = 132;
        p.Members[1].Items[0] = 133;
        Assert.True(T.Outdoor.CharonItems.All(p.HasItem));
    }

    // --- Saves --------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void ALoopSavedMidQuestion_GoesOnTheSame()
    {
        var party = Party();
        var o = Loop(party, new ScriptRng(1, 50, 1));
        o.Arrive(9, 9, 10);
        var snap = o.Capture();
        var copy = Loop(party, new ScriptRng());
        copy.Restore(snap);
        Assert.Equal(P4OutdoorPrompt.Confirm, copy.Prompt);
        copy.Answer(true);
        Assert.Equal(new P4OutdoorFight(true, false, false, false, 0), copy.PendingFight);
    }

    // --- Towns --------------------------------------------------------------------------------------------------------

    private static P4TownLoop Town(P4Party party, GameOptions? o = null) => new(o ?? Manual, T, Atlas, party, new ScriptRng());

    [OriginalDataFact]
    public void TownEntry_Manual_DepositsGold_ThenSharesExperience()
    {
        var party = Party();
        party.ExperiencePool = 90; // 900 experience
        var w = Town(party);
        w.Arrive(2);
        Assert.Equal((P4TownPrompt.Shares, P4ShareKind.Gold), (w.Prompt, w.SharesFor));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.EnterShares([0, 1, 1])); // an Okay member takes 1–3
        w.EnterShares([2, 1, 1]);
        Assert.Equal(0, party.Gold);
        Assert.Equal([2498L, 1249L, 1249L], party.Members.Select(m => m.Account)); // 5000 / 4.0001 per share
        Assert.Equal(P4ShareKind.Experience, w.SharesFor);
        Assert.Equal(new P4GateShare(900, null), w.GateShare); // s165: _gatebank's window while the shares are asked
        w.EnterShares([1, 1, 1]);
        Assert.Equal([299L, 299L, 299L], party.Members.Select(m => m.Experience)); // 900 / 3.0001, the rest lost
        // s165: _gatebank writes each gain over its share and waits for SPACE (0x529E) before the aging.
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Equal([299L, 299L, 299L], w.GateShare!.Gains!);
        w.Continue();
        Assert.Null(w.GateShare);
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
        Assert.All(party.Members, m => Assert.Equal(2, m.Town));
        Assert.Equal(8, party.ShopStock.Sum()); // _igen8: 8 random items added to the live stock
    }

    [OriginalDataFact]
    public void TownEntry_Code_LeavesTheGold()
    {
        var party = Party();
        var w = Town(party, Code);
        w.Arrive(2);
        Assert.Equal(P4TownPrompt.Menu, w.Prompt);
        Assert.Equal(5000, party.Gold);
    }

    [OriginalDataFact]
    public void TheDoors_DependOnTheTownType()
    {
        var w = Town(Party(), Code);
        w.Arrive(1);
        Assert.Contains(P4Building.HallOfGlory, w.Buildings);
        w.Arrive(2);
        Assert.Contains(P4Building.Shipwright, w.Buildings);
        w.Arrive(4);
        Assert.Equal(6, w.Buildings.Count);
    }

    [OriginalDataFact]
    public void TheInn_Heals_AndUnderTheManualTakesTwoWeeks()
    {
        var party = Party();
        party.Members[0].Hp = 3;
        party.Members[0].Wounds[(int)P4BodyPart.LeftArm] = 2;
        party.Members[1].Items[0] = P4Party.RingA;
        var w = Town(party, Code);
        w.Arrive(2);
        w.Enter(P4Building.Inn);
        Assert.Equal(30, party.Members[0].Hp);
        Assert.Equal(0, party.Members[0].Wounds[(int)P4BodyPart.LeftArm]);
        Assert.Equal(20, party.Members[1].ItemCharges[0]);
        Assert.Equal(0, party.DaysOut);

        var manual = Party();
        manual.Gold = 0;
        var m = Town(manual);
        m.Arrive(2);
        m.Enter(P4Building.Inn);
        Assert.Equal(T.Towns.TownInnDaysManual, manual.DaysOut);
    }

    [OriginalDataFact]
    public void TheShipwright_SellsAShip_MooredAtTheTownsShipSquare()
    {
        var party = Party();
        var w = Town(party, Code);
        w.Arrive(2);
        w.Enter(P4Building.Shipwright);
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        Assert.Equal("_noyes", w.IconMenu); // s138 / s140: _shipyard asks with _noyes's YES / NO box, not _confirm's panel
        var back = Town(party, Code);
        back.Restore(w.Capture()); // a save at the question keeps the box
        Assert.Equal((P4TownPrompt.Confirm, "_noyes"), (back.Prompt, back.IconMenu));
        w.Answer(true);
        Assert.Null(w.IconMenu);
        Assert.Equal(3500, party.Gold);
        Assert.Equal((P4ShipState.Moored, 8, 17, 5), (party.Ship, party.ShipMap, party.ShipX, party.ShipY));
        // s151: "Thank you very much." and _spc_wait, then the street.
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Equal(GameText.S("p4x.0x40147"), w.Box.Last(l => l.Length > 0));
        w.Continue();
        Assert.Equal((P4TownPrompt.Menu, P4TownScene.Street), (w.Prompt, w.Scene));
    }

    [OriginalDataFact]
    public void TheShipwright_PrintsTheOffer_AsTheOriginalsLines()
    {
        // s151 (_shipyard 0x5FBC-0x6054): without a ship the question ends the offer's line; with one, "If you buy a ship,"
        // ends it and the warning and the question take the next two lines. "No" prints "Come again." and waits.
        var party = Party();
        var w = Town(party, Code);
        w.Arrive(2);
        w.Enter(P4Building.Shipwright);
        Assert.Equal([GameText.S("p4x.0x400f4") + GameText.S("p4x.0x4010d") + GameText.S("p4x.0x40139")],
            w.Box.Where(l => l.Length > 0));
        w.Answer(false);
        Assert.Equal(GameText.S("p4x.0x40160"), w.Box.Last(l => l.Length > 0));
        w.Continue();
        party.Ship = P4ShipState.Moored;
        w.Enter(P4Building.Shipwright);
        Assert.Equal([GameText.S("p4x.0x400f4") + GameText.S("p4x.0x4010d") + GameText.S("p4x.0x40110"),
            GameText.S("p4x.0x4011b"), GameText.S("p4x.0x40139")], w.Box.Where(l => l.Length > 0));
    }

    [OriginalDataFact]
    public void Leaving_PutsThePartyOnTheExitSquare()
    {
        var party = Party();
        party.Daytime = 7;
        var w = Town(party, Code);
        w.Arrive(1);
        w.Enter(P4Building.Leave);
        Assert.Equal(P4TownPrompt.Left, w.Prompt);
        Assert.Equal(new P4Transfer(P4Transfer.Outdoors, 9, 9, 10, 0), w.Transfer);
        Assert.Equal(0, party.Daytime);
    }

    [OriginalDataFact]
    public void TheMystic_ScoresTheParty()
    {
        var party = Party();
        party.StoryFlags[4] = 1;
        party.Pack[3] = P4TownLoop.ZeusJuice;
        var w = Town(party, Code);
        w.Arrive(2);
        w.Enter(P4Building.Mystic);
        // 3 × 1 + 15 × 1 + 2 × 3 = 24: "A mere fledgling!"
        Assert.Contains(w.Box, l => l.Contains("24"));
        Assert.Contains(GameText.S("p4x.0x4024f"), w.Box);
        Assert.Equal(3, party.AverageLevel);
    }

    [OriginalDataFact]
    public void Aging_TurnsDaysIntoAge_AtTheGate()
    {
        var party = Party();
        party.DaysOut = 0x700 * 3 + 5;
        var w = Town(party, Code);
        w.Arrive(2);
        Assert.All(party.Members, m => Assert.Equal(3, m.AgeUnits));
        Assert.Equal(0, party.DaysOut);
    }

    [OriginalDataFact]
    public void Town_TableHoldsEveryExit_OnItsOutdoorMap()
    {
        foreach (var t in Atlas.Towns.Values)
        {
            Assert.True(Atlas.Outdoors.ContainsKey(t.ExitMap), t.Name);
            Assert.Equal(t.ShipX is null, t.Id is 1 or 4 or 15);
        }
        Assert.Equal(new P4TownSite(1, "Pendragon", 0, 9, 9, 10, null, null), Atlas.Towns[1]);
    }
}
