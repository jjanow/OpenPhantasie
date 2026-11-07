using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's location maps (T6.3; <c>reverse-engineering/x68000/phantasie4/logic-specs/events.md</c>): the movement
/// map (<c>_mkdin</c>), the move buttons and doors (<c>_dmain</c>, <c>_dmove</c>, <c>_try_ul</c>), the <c>O_0xx</c>
/// interpreter (<c>_exe_ev</c>), traps and chests, the rotating flag reset, random encounters, Examine and saving
/// mid-event. Synthetic maps test single rules; the shipped files test the real scripts.
/// </summary>
public class Phantasie4DungeonTests
{
    private static readonly IReadOnlyDictionary<int, P4DungeonFile> Files = Phantasie4World.LoadDungeonFiles();
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    /// <summary>Every draw returns <paramref name="value"/>, clamped into the asked range (100 = every roll fails).</summary>
    private sealed class Fixed(int value) : IRandomSource
    {
        public int Draws { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            return Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    /// <summary>Hands out the given values in order; each must lie in the asked range.</summary>
    private sealed class Script(params int[] values) : IRandomSource
    {
        private int _i;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.True(_i < values.Length, "script ran out");
            var v = values[_i++];
            Assert.InRange(v, minInclusive, maxExclusive - 1);
            return v;
        }

        public bool Done => _i == values.Length;
    }

    private static P4Party Party(int members = 2, int skill = 50)
    {
        var p = new P4Party();
        for (var i = 0; i < members; i++)
        {
            var m = new P4Member { Name = $"M{i}", Level = 3, HpMax = 20, Hp = 20, Defence = 10 };
            Array.Fill(m.Skills, skill);
            p.Members.Add(m);
        }
        return p;
    }

    private static P4Dungeon Dungeon(P4Party party, IRandomSource rng, GameOptions? options = null,
        IReadOnlyDictionary<int, P4DungeonFile>? files = null)
    {
        var f = files ?? Files;
        return new P4Dungeon(options ?? Manual, T, party, rng, n => f[n], i => $"item{i}");
    }

    // --- Synthetic maps ------------------------------------------------------------------------------------------------

    /// <summary>A 30 × 30 map open everywhere (north picture 31, west wall 7, floor 0) with the given event code.</summary>
    private static P4DungeonFile OpenMap(int map, IReadOnlyList<P4EventTrigger> triggers, byte[] code,
        Action<ushort[]>? edit = null)
    {
        var words = new ushort[31 * 31];
        for (var i = 0; i < words.Length; i++)
            words[i] = (31 << 6) | (7 << 11);
        edit?.Invoke(words);
        return new P4DungeonFile(map, "test", words, triggers, code);
    }

    private static ushort Word(int floor, int north, int west) => (ushort)(floor | (north << 6) | (west << 11));

    private static Dictionary<int, P4DungeonFile> One(P4DungeonFile f) => new() { [f.Map] = f };

    /// <summary>Load event: NOENC; END — no random rolls.</summary>
    private static readonly byte[] Quiet = [0x22, 0x00];

    private static readonly P4EventTrigger[] LoadOnly = [new(0x20, 0, 4, 0)];

    [OriginalDataFact]
    public void MovementMap_FollowsTheWallPicturesFloorsAndEdges()
    {
        var f = OpenMap(5, LoadOnly, Quiet, w =>
        {
            w[10 * 31 + 10] = Word(0, 1, 7); // (10,10): north picture 1 = wall
            w[10 * 31 + 12] = Word(0, 31, 1); // (12,10): west wall value 1 = wall
            w[11 * 31 + 20] = Word(4, 31, 7); // (20,11): floor 4 blocks entry
            w[5 * 31 + 5] = Word(0, 16, 7); // (5,5): gate (16) counts as open
        });
        var m = new P4DungeonMap(f);
        Assert.Equal(P4DungeonMap.Wall, m.Move(10, 10, 0));
        Assert.Equal(P4DungeonMap.Wall, m.Move(10, 9, 2)); // the same wall seen from the north
        Assert.Equal(P4DungeonMap.Wall, m.Move(12, 10, 3));
        Assert.Equal(P4DungeonMap.Wall, m.Move(11, 10, 1));
        Assert.Equal(P4DungeonMap.Wall, m.Move(20, 10, 2)); // into floor 4
        Assert.Equal(P4DungeonMap.Open, m.Move(20, 11, 2)); // out of it
        Assert.Equal(P4DungeonMap.Open, m.Move(5, 5, 0));
        Assert.Equal((7, 7, 7, 7), (m.Move(0, 3, 3), m.Move(3, 0, 0), m.Move(29, 3, 1), m.Move(3, 29, 2)));
    }

    [OriginalDataFact]
    public void MovementMap_ShippedMaps_OnlyOpenWallOrEdge()
    {
        foreach (var f in Files.Values)
        {
            var m = new P4DungeonMap(f);
            Assert.All(m.Moves, w =>
            {
                for (var d = 0; d < 4; d++)
                    Assert.Contains((w >> (4 * d)) & 0xf, new[] { 0, 1, 7 });
            });
        }
    }

    [OriginalDataFact]
    public void LoadResetsOneMapsFlagsInRotation()
    {
        var f = OpenMap(3, LoadOnly, Quiet);
        var party = Party();
        party.LocationFlag(0, 1) = 1;
        party.LocationFlag(21, 2) = 1;
        party.LocationFlag(20, 3) = 1;
        party.LocationFlag(3, 4) = 1;
        var d = Dungeon(party, new Fixed(100), files: One(f));
        d.Enter(3, 5, 5, 0); // counter 0: clears map 0, → 21
        Assert.Equal((0, 21), (party.LocationFlag(0, 1), party.StoryFlags[0]));
        d.Enter(3, 5, 5, 0); // clears 21 → 20
        d.Enter(3, 5, 5, 0); // clears 20 → 19
        Assert.Equal((0, 0, 1, 19), (party.LocationFlag(21, 2), party.LocationFlag(20, 3), party.LocationFlag(3, 4), party.StoryFlags[0]));
        party.StoryFlags[0] = 1;
        d.Enter(3, 5, 5, 0);
        Assert.Equal(21, party.StoryFlags[0]); // below 1 → 21
    }

    [OriginalDataFact]
    public void Diagonal_IsTwoStepsWithTheSecondQueued_FacingTurnsOnTheSecond()
    {
        var f = OpenMap(4, LoadOnly, Quiet);
        var d = Dungeon(Party(), new Fixed(100), files: One(f));
        d.Enter(4, 5, 5, 2);
        d.Move(8); // NE: E now (facing kept), then N queued for the next pass
        Assert.Equal((6, 4, 0), (d.X, d.Y, d.Dir));
        Assert.Equal((6, 5), (d.OldX, d.OldY));
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
    }

    [OriginalDataFact]
    public void Diagonal_BlockedFirstStep_GoesTheOtherWayRound()
    {
        var f = OpenMap(4, LoadOnly, Quiet, w => w[5 * 31 + 6] = Word(0, 31, 1)); // wall west of (6,5)
        var d = Dungeon(Party(), new Fixed(100), files: One(f));
        d.Enter(4, 5, 5, 2);
        d.Move(8); // E blocked → N now, E queued
        Assert.Equal((6, 4, 1), (d.X, d.Y, d.Dir));
    }

    [OriginalDataFact]
    public void LockedDoor_PickedOnADiagonal_SealsTheFacedSideAndDropsTheQueuedStep()
    {
        // Load: WALL2 5,5,E (a locked door east of (5,5)); NOENC.
        byte[] code = [0x1B, 5, 5, 1, 0x22, 0x00];
        var f = OpenMap(4, LoadOnly, code);
        var d = Dungeon(Party(2, skill: 50), new Fixed(1), files: One(f)); // every roll succeeds
        d.Enter(4, 5, 5, 2);
        d.Move(8); // NE: the facing (S) is not turned for the first step
        Assert.Equal(P4DungeonPrompt.Wait, d.Prompt);
        Assert.Equal("M1 forced the lock.", d.Box.Single());
        Assert.Equal(P4DungeonMap.SealedDoor, d.Map.Move(5, 5, 2)); // the FACED side (S) became 3 …
        Assert.Equal(P4DungeonMap.LockedDoor, d.Map.Move(5, 5, 1)); // … not the door
        d.Continue();
        Assert.Equal((6, 5, 2), (d.X, d.Y, d.Dir)); // moved E; _ewait dropped the queued N step
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
    }

    [OriginalDataFact]
    public void SealedDoor_NeverOpens_AndThePartyStays()
    {
        byte[] code = [0x2E, 5, 5, 1, 0x22, 0x00]; // WALL3 5,5,E
        var f = OpenMap(4, LoadOnly, code);
        var rng = new Fixed(1);
        var d = Dungeon(Party(), rng, files: One(f));
        d.Enter(4, 5, 5, 0);
        d.Move(5);
        Assert.Equal("The lock won't open.", d.Box.Single());
        Assert.Equal(2, rng.Draws); // both members still roll
        d.Continue();
        Assert.Equal((5, 5, 1), (d.X, d.Y, d.Dir));
    }

    [OriginalDataFact]
    public void Edge_RunsTheEdgeTrigger_TeleportOutdoorsLeaves()
    {
        // 0x21: TELEPORT 1, 7, 12, 13, 2; END
        byte[] code = [0x22, 0x00, 0x1E, 1, 7, 12, 13, 2, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(0x21, 0, 4, 2)], code);
        var d = Dungeon(Party(), new Fixed(100), files: One(f));
        d.Enter(4, 0, 5, 0);
        d.Move(3);
        Assert.Equal(P4DungeonPrompt.Left, d.Prompt);
        Assert.Equal(new P4Transfer(P4Transfer.Outdoors, 7, 12, 13, 2), d.Transfer);
    }

    [OriginalDataFact]
    public void Gold_EventBugAddsAnother990000_UnderTheCodeCap_ManualCapsAt65000()
    {
        // Square (5,5): GIVEGOLD 20; END
        byte[] code = [0x22, 0x00, 0x13, 20, 0, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var party = Party();
        party.Gold = 989990;
        var d = Dungeon(party, new Fixed(100), Code, One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(["20 GP", "obtained."], d.Box);
        Assert.Equal(1980010, party.Gold);

        var manual = Party();
        manual.Gold = 64990;
        Dungeon(manual, new Fixed(100), Manual, One(f)).Enter(4, 5, 5, 0);
        Assert.Equal(65000, manual.Gold);
    }

    [OriginalDataFact]
    public void FloorTrap_SpottedAndDisarmed()
    {
        // (5,5): TRAP 0; JF end; SETLF 1; END
        byte[] code = [0x22, 0x00, 0x30, 0, 0x03, 9, 0, 0x15, 1, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var party = Party(2, skill: 40);
        // spot: M0 100 (no), M1 40 (yes) → menu; disarm by M0: 40 ≤ 40 → yes; then the random roll 100 (none)
        var rng = new Script(100, 40, 40, 100);
        var d = Dungeon(party, rng, files: One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(P4DungeonPrompt.Menu, d.Prompt);
        Assert.Equal("M1 found a trap.", d.Box.Single());
        Assert.Equal(["Disarm the trap", "Turn back"], d.MenuOptions);
        d.Choose(1);
        Assert.Equal(P4DungeonPrompt.Who, d.Prompt);
        d.Choose(0);
        Assert.Equal("M0 succeeded.", d.Box[^1]);
        d.Continue();
        Assert.Equal(1, party.LocationFlag(4, 1)); // tff = 1: the script went on
        Assert.True(rng.Done);
    }

    [OriginalDataFact]
    public void FloorTrap_NobodySpotsIt_ItGoesOffAndMembersMayDodge()
    {
        byte[] code = [0x22, 0x00, 0x30, 0, 0x00];
        var f = OpenMap(10, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var party = Party(2, skill: 10);
        party.Members[0].Defence = 50;
        // spot 100, 100 → off; M0 dodge 49 < 50; M1 dodge 100 → rand(5) = 4 × (10/5 + 1) = 12; random roll 100
        var rng = new Script(100, 100, 49, 100, 4, 100);
        var d = Dungeon(party, rng, files: One(f));
        d.Enter(10, 5, 5, 0);
        Assert.Equal("A trap!", d.Box.Single());
        d.Continue();
        Assert.Equal(["M0 dodged it.", "M1 took 12 damage."], d.Box);
        Assert.Equal(8, party.Members[1].Hp);
        d.Continue();
        Assert.True(rng.Done);
    }

    [OriginalDataFact]
    public void Chest_LockedChest_OpensWithPickLock_AndBecomesFloor6()
    {
        // (5,5): CHEST 1; JF end; GIVEITEM 77; END
        byte[] code = [0x22, 0x00, 0x24, 1, 0x03, 8, 0, 0x11, 77, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var party = Party(2, skill: 30);
        var rng = new Script(30, 31, 100, 100); // lock: M0 yes, M1 no; GIVEITEM's pack has room; the random roll; next pass
        var d = Dungeon(party, rng, files: One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal("There is a chest.", d.Box.Single());
        d.Continue();
        Assert.Equal(["There is a chest.", "M0 forced the lock."], d.Box);
        Assert.Equal(P4DungeonMap.OpenedChest, d.Map.Floor(5, 5));
        d.Continue();
        Assert.Equal(["item77", "obtained."], d.Box);
        Assert.Equal(77, party.Pack[0]);
        d.Continue();
        // Another action on the square: the chest is opened, tff = 0, nothing more.
        d.Act(P4DungeonCommand.Other);
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
        Assert.Equal(1, party.Pack.Count(i => i == 77));
    }

    [OriginalDataFact]
    public void HiddenItem_UsesTheSpotTrapByte_AndTheManualExamineBonus()
    {
        // (5,5): CHEST 3; JF end; SETLF 0; END
        byte[] code = [0x22, 0x00, 0x24, 3, 0x03, 9, 0, 0x15, 0, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var party = Party(1, skill: 10);
        party.Members[0].Skills[(int)P4Skill.SpotItem] = 100;
        var rng = new Script(30, 100, 30, 100); // pass 1: 30 > 10 (none), roll; pass 2 after E: 30 ≤ 10 + 20, …
        var d = Dungeon(party, rng, Manual, One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(0, party.LocationFlag(4, 0));
        d.Act(P4DungeonCommand.Examine);
        Assert.Equal("M0 found an item.", d.Box.Single());
        d.Continue();
        Assert.Equal(1, party.LocationFlag(4, 0));
        Assert.True(rng.Done);
    }

    [OriginalDataFact]
    public void RandomEncounter_AfterEveryEvent_FromThePool()
    {
        // load: POOLCLR; POOLADD 9; POOLADD 11; ENCPCT 5; END (encounters stay on)
        byte[] code = [0x2D, 0x2B, 9, 0x2B, 11, 0x2C, 5, 0x00];
        var f = OpenMap(4, LoadOnly, code);
        var rng = new Script(100, 5, 2); // load's roll: none; pass at (5,5): 5 ≤ 5 → pool entry 2 − 1
        var d = Dungeon(Party(), rng, files: One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(P4DungeonPrompt.Fight, d.Prompt);
        Assert.Equal(new P4Fight(11, 1, false), d.PendingFight);
        d.FightOver(partyLost: false);
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
        Assert.True(rng.Done);
    }

    [OriginalDataFact]
    public void NoEncounterOrFightOnWater_AndWaterHurtsPoorSwimmers()
    {
        // (5,5) is floor 27; its event: FIGHT 3; END (refused on water)
        byte[] code = [0x22, 0x00, 0x23, 3, 0x00];
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code, w => w[5 * 31 + 5] = Word(27, 31, 7));
        var party = Party(1, skill: 40);
        var rng = new Script(3, 41, 100); // swim: d = rand(4) + 1 = 4, 41 > 40 → hurt; the event's random roll
        var d = Dungeon(party, rng, files: One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(16, party.Members[0].Hp);
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
        Assert.Null(d.PendingFight);
        Assert.True(rng.Done);
    }

    [OriginalDataFact]
    public void Damage_WipingTheParty_EndsTheGame()
    {
        byte[] code = [0x22, 0x00, 0x1A, 50, 0x00]; // DAMAGE 50
        var f = OpenMap(4, [new(0x20, 0, 4, 0), new(5, 5, 4, 2)], code);
        var d = Dungeon(Party(2), new Fixed(50), files: One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(P4DungeonPrompt.PartyLost, d.Prompt);
        Assert.Equal("The party has been wiped out.", d.Box[0]);
    }

    [OriginalDataFact]
    public void ExamineWalk_Code_HasItsClearingBug_AndQueuesTheLeastVisitedSide()
    {
        var f = OpenMap(4, LoadOnly, Quiet, w => w[7] = Word(0, 31, 1)); // (7,0): a wall on its west side
        var d = Dungeon(Party(), new Fixed(400), Code, One(f));
        d.Enter(4, 5, 5, 0);
        Assert.Equal(P4DungeonMap.Wall, d.Map.Move(7, 0, 3));
        d.Act(P4DungeonCommand.Examine);
        Assert.Equal(P4DungeonMap.Open, d.Map.Move(7, 0, 3)); // _seainit zeroed the S and W codes of (7,0)
        // The walk runs its 63 passes without a key (nothing here stops it) and leaves the party elsewhere.
        Assert.Equal(0, d.ExamineSteps);
        Assert.NotEqual((5, 5), (d.X, d.Y));
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
    }

    [OriginalDataFact]
    public void Snapshot_MidMenu_RestoresAndAnswersTheSame()
    {
        var party = Party();
        var d = Dungeon(party, new Fixed(100));
        d.Enter(2, 8, 17, 0);
        for (var i = 0; i < 4; i++)
            d.Continue();
        Assert.Equal(P4DungeonPrompt.Menu, d.Prompt);
        var snap = d.Capture();
        var restored = Dungeon(party, new Fixed(100));
        restored.Restore(snap);
        Assert.Equal(d.MenuOptions, restored.MenuOptions);
        restored.Choose(3); // free only the Half-Orc
        Assert.Equal(P4DungeonMap.Open, restored.Map.Move(10, 17, 0));
        Assert.Equal(1, party.StoryFlags[11]);
    }

    // --- The shipped scripts --------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void O002_ThreeCells_OpenAll_FreesEveryoneThenTheFight()
    {
        var party = Party();
        var d = Dungeon(party, new Fixed(100));
        d.Enter(2, 8, 17, 0);
        Assert.Equal(P4DungeonMap.SealedDoor, d.Map.Move(10, 17, 0)); // sealed by the load event
        Assert.Equal(4, d.EncounterRate);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 5, 2, 7], d.Pool);
        Assert.Equal(P4DungeonPrompt.Wait, d.Prompt);
        for (var i = 0; i < 4; i++)
            d.Continue();
        Assert.Equal(P4DungeonPrompt.Menu, d.Prompt);
        Assert.Equal(4, d.MenuOptions.Count);
        d.Choose(2);
        Assert.Equal(1, party.StoryFlags[11]);
        Assert.Equal((P4DungeonMap.Open, 19), (d.Map.Move(10, 17, 0), d.Map.NorthPicture(10, 17)));
        d.Continue();
        Assert.Equal(P4DungeonMap.Open, d.Map.Move(14, 20, 2));
        d.Continue();
        Assert.Equal(P4DungeonPrompt.Fight, d.Prompt);
        Assert.Equal(16, d.PendingFight!.Encounter);
        d.FightOver(partyLost: false);
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);

        // Story flag 11 makes it permanent: the cells are empty now.
        d.Act(P4DungeonCommand.Other);
        Assert.Equal(P4DungeonPrompt.Wait, d.Prompt);
        d.Continue();
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
    }

    [OriginalDataFact]
    public void O002_DontPress_LeavesTheCellsSealed()
    {
        var party = Party();
        var d = Dungeon(party, new Fixed(100));
        d.Enter(2, 8, 17, 0);
        for (var i = 0; i < 4; i++)
            d.Continue();
        d.Choose(1);
        Assert.Equal(0, party.StoryFlags[11]);
        Assert.Equal(P4DungeonMap.SealedDoor, d.Map.Move(10, 17, 0));
        Assert.Equal(P4DungeonPrompt.Action, d.Prompt);
    }

    [OriginalDataFact]
    public void O002_TeleportToAnotherDungeonMap_LoadsIt()
    {
        var d = Dungeon(Party(), new Fixed(100));
        d.Enter(2, 9, 25, 0); // (9,25) any: TELEPORT 2, 1, 10, 25, 1
        Assert.Equal(1, d.Map.Map);
        Assert.Equal((10, 25, 1), (d.X, d.Y, d.Dir));
    }

    /// <summary>
    /// Every trigger of the 21 shipped files runs on its square and facing, with every roll high and every roll low, the
    /// prompts answered by rote (first menu line, first member, "JILL", 1, fights won) — nothing may throw.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(100)]
    [InlineData(1)]
    public void EveryShippedTrigger_Runs(int roll)
    {
        var runs = 0;
        foreach (var f in Files.Values)
            foreach (var t in f.Triggers.Where(t => t.X < 30))
            {
                var party = Party(3, skill: 50);
                party.Gold = 5000;
                var d = Dungeon(party, new Fixed(roll));
                d.Enter(f.Map, t.X, t.Y, t.Dir == P4EventTrigger.AnyDirection ? 0 : t.Dir);
                for (var i = 0; i < 300 && d.Prompt is not (P4DungeonPrompt.Action or P4DungeonPrompt.Left
                         or P4DungeonPrompt.PartyLost); i++)
                {
                    switch (d.Prompt)
                    {
                        case P4DungeonPrompt.Wait: d.Continue(); break;
                        case P4DungeonPrompt.Menu: d.Choose(1); break;
                        case P4DungeonPrompt.Who: d.Choose(0); break;
                        case P4DungeonPrompt.Letters: d.EnterText("JILL"); break;
                        case P4DungeonPrompt.Number: d.EnterNumber(1); break;
                        case P4DungeonPrompt.Fight: d.FightOver(partyLost: false); break;
                    }
                }
                Assert.InRange(d.Pool.Count, 0, P4Dungeon.PoolSize);
                runs++;
            }
        Assert.True(runs > 1000, $"{runs} triggers run");
    }

    /// <summary>Operand bytes per opcode (<c>events.md</c> §4).</summary>
    private static int Length(int op) => op switch
    {
        0x00 or 0x08 or 0x0B or 0x0C or 0x0E or 0x21 or 0x22 or 0x27 or 0x29 or 0x2D or 0x2F or 0x31 or 0x39 => 1,
        0x10 or 0x11 or 0x14 or 0x15 or 0x16 or 0x17 or 0x18 or 0x19 or 0x1A or 0x1F or 0x20 or 0x23 or 0x24 or 0x25
            or 0x26 or 0x2B or 0x2C or 0x30 or 0x32 or 0x34 or 0x35 or 0x36 => 2,
        0x01 or 0x02 or 0x03 or 0x04 or 0x05 or 0x06 or 0x07 or 0x09 or 0x0A or 0x0D or 0x0F or 0x12 or 0x13 or 0x1D
            or 0x28 or 0x33 or 0x37 or 0x38 => 3,
        0x1B or 0x1C or 0x2A or 0x2E or 0x3A => 4,
        0x1E => 6,
        _ => 1,
    };

    [OriginalDataFact]
    public void EveryReachableTextHasAnEnglishString()
    {
        var missing = new List<string>();
        var reached = 0;
        foreach (var f in Files.Values)
        {
            var seen = new HashSet<int>();
            var todo = new Stack<int>(f.Triggers.Select(t => t.Address));
            while (todo.Count > 0)
            {
                var pc = todo.Pop();
                while (pc < f.Code.Count && seen.Add(pc))
                {
                    var op = f.Code[pc];
                    int W(int at) => f.Code[at] | (f.Code[at + 1] << 8);
                    if (op is 0x09 or 0x0A or 0x28)
                    {
                        reached++;
                        var id = f.TextId(W(pc + 1));
                        if (!GameTextSetup.PhantasieIV.Contains(id))
                            missing.Add(id);
                    }
                    if (op is >= 0x01 and <= 0x07)
                        todo.Push(W(pc + 1));
                    if (op == 0x2A)
                        todo.Push(W(pc + 2));
                    if (op is 0x00 or 0x01 or 0x08)
                        break;
                    pc += Length(op);
                }
            }
        }
        Assert.Empty(missing);
        Assert.True(reached > 600, $"only {reached} texts reached");
    }
}
