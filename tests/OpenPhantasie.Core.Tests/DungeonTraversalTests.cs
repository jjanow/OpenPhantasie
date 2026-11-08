using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class DungeonTraversalTests
{
    private static DungeonTraversal Traversal =>
        GameDataRepository.LoadPhantasie1().CreateDungeonTraversal();

    [OriginalDataFact]
    public void Geometry_Is33x38()
    {
        Assert.Equal(33, DungeonLevel.Width);
        Assert.Equal(38, DungeonLevel.Height);
        Assert.Equal(1254, DungeonLevel.GridByteLength);
        Assert.Equal(1254, GameDataRepository.DungeonGridByteLength);

        var data = GameDataRepository.LoadPhantasie1();
        var dng1 = Assert.Single(data.Dungeons, d => d.Id == "DNG1");
        Assert.NotNull(dng1.Cells);
        Assert.Equal(33, dng1.Cells!.Width);
        Assert.Equal(38, dng1.Cells.Height);
        Assert.Equal(0xd7, dng1.Cells[20, 0].FeatureId);
    }

    [OriginalDataFact]
    public void EnterDungeon1_FromOverworldDest_StandsOnExitTile()
    {
        var traversal = Traversal;
        var state = new DungeonState();
        traversal.Enter(state, levelId: 1, destX: 20, destY: 0);

        Assert.Equal(1, state.LevelId);
        Assert.Equal(20, state.X);
        Assert.Equal(0, state.Y);
        Assert.Equal(0xd7, traversal.GetLevel(1).Get(20, 0));
        Assert.Equal(DungeonFeatureKind.ExitOrStairs, DungeonLevel.Classify(0xd7));
    }

    [OriginalDataFact]
    public void MoveIntoWall_PrintsThatIsTheWall()
    {
        var traversal = Traversal;
        var state = new DungeonState();
        traversal.Enter(state, 1, 20, 0);

        // West of entrance is wall (0)
        var result = traversal.TryMove(state, CardinalDirection.West);
        Assert.Equal(DungeonMoveKind.BlockedWall, result.Kind);
        Assert.Equal("THAT IS THE WALL", result.Message);
        Assert.Equal(20, state.X);
        Assert.Equal(0, state.Y);
    }

    [OriginalDataFact]
    public void StairsCell_OnlyPrintsTheDosText_s49()
    {
        // OV2 0x2951: sprintf "`core.dungeon.stairs_from_level_x_to_x` %-2d TO %-2d" (b − 0xd9, b − 0xd8), print, return 3 — no level change.
        var traversal = Traversal;
        var state = new DungeonState { LevelId = 5, Level = traversal.GetLevel(5).CloneAsBuffer(), X = 4, Y = 7 };
        Assert.Equal(0xda, state.Level.Get(4, 8));
        var r = traversal.TryMoveAbsolute(state, 4, 8);
        Assert.Equal(DungeonMoveKind.TriggerStairs, r.Kind);
        Assert.Equal("STAIRS FROM LEVEL 1  TO 2", r.Message);
        Assert.Equal((5, 4, 8), (state.LevelId, state.X, state.Y));
    }

    [OriginalDataFact]
    public void MoveSouthFromEntrance_EntersCorridor()
    {
        var traversal = Traversal;
        var state = new DungeonState();
        traversal.Enter(state, 1, 20, 0);

        var result = traversal.TryMove(state, CardinalDirection.South);
        Assert.Equal(20, state.X);
        Assert.Equal(1, state.Y);
        Assert.NotEqual(DungeonMoveKind.BlockedWall, result.Kind);
        Assert.True(traversal.GetLevel(1).IsWalkable(20, 1));
    }

    [OriginalDataFact]
    public void ForwardFacing_MatchesCardinalSouth()
    {
        var traversal = Traversal;
        var state = new DungeonState();
        traversal.Enter(state, 1, 20, 0);
        state.Facing = DungeonFacing.South;

        var result = traversal.TryMoveForward(state);
        Assert.Equal(1, state.Y);
        Assert.NotEqual(DungeonMoveKind.BlockedWall, result.Kind);
    }

    [OriginalDataFact]
    public void AllTenLevels_LoadAndHaveWalkableCells()
    {
        var traversal = Traversal;
        Assert.Equal(10, traversal.LevelIds.Count);
        foreach (var id in traversal.LevelIds)
        {
            var level = traversal.GetLevel(id);
            Assert.Equal(DungeonLevel.GridByteLength, level.Cells.Length);
            Assert.Contains(level.Cells, b => b != 0);
        }
    }

    [Fact]
    public void FeatureClassify_MatchesFun2249Ranges()
    {
        Assert.Equal(DungeonFeatureKind.Wall, DungeonLevel.Classify(0));
        Assert.Equal(DungeonFeatureKind.Floor, DungeonLevel.Classify(1));
        Assert.Equal(DungeonFeatureKind.Trap, DungeonLevel.Classify(3));
        Assert.Equal(DungeonFeatureKind.Encounter, DungeonLevel.Classify(0x0c));
        Assert.Equal(DungeonFeatureKind.Message, DungeonLevel.Classify(0x8f));
        Assert.Equal(DungeonFeatureKind.LockOrAmbush, DungeonLevel.Classify(0xc6));
        Assert.Equal(DungeonFeatureKind.ExitOrStairs, DungeonLevel.Classify(0xd7));
        Assert.Equal(DungeonFeatureKind.StairsMessage, DungeonLevel.Classify(0xda));
        Assert.Equal(DungeonFeatureKind.ScriptedEvent, DungeonLevel.Classify(0xde));
    }

    [OriginalDataFact]
    public void StepOnExitTile_SignalsExitDungeon()
    {
        var traversal = Traversal;
        var state = new DungeonState();
        // Stand south of entrance, then step north back onto 0xd7
        traversal.Enter(state, 1, 20, 0);
        traversal.TryMove(state, CardinalDirection.South);
        var result = traversal.TryMove(state, CardinalDirection.North);
        Assert.Equal(DungeonMoveKind.ExitDungeon, result.Kind);
        Assert.Equal(0xd7, result.FeatureByte);
    }

    [OriginalDataFact]
    public void Trap_DamagesPartyMemberWhenUnspotted()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var hero = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "TrapGuy",
                Race = Race.Human,
                Class = CharacterClass.Fighter,
            },
            data.CreationTables,
            new SeededDosRandomSource(1));
        // Spot 0: only a roll of 0 spots it (rand0_99 ≤ @36, 0x2fbc); seed 99 does not
        hero.Skills = hero.Skills with { SpotTrap = 0, DisarmTrap = 0 };
        var party = new Party();
        party.Members.Add(hero);
        var hpBefore = hero.HpCur;

        // Build a tiny synthetic level with a trap at (1,0)
        var cells = new byte[DungeonLevel.GridByteLength];
        cells[0] = 1; // floor start
        cells[1] = 3; // trap
        var level = new DungeonLevel(99, "TEST", cells);
        var traversal = new DungeonTraversal([level]);
        var state = new DungeonState { LevelId = 99, X = 0, Y = 0 };
        var result = traversal.TryMove(state, CardinalDirection.East, party, new SeededDosRandomSource(99));

        Assert.Equal(DungeonMoveKind.TriggerTrap, result.Kind);
        Assert.True(hero.HpCur < hpBefore);
        Assert.NotNull(result.Damage);
    }

    [OriginalDataFact]
    public void OverworldEnterDungeon_DestMatchesDng1Entrance()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var ow = data.CreateTraversal();
        var state = new OverworldState { RegionId = 1, X = 8, Y = 8 };
        var result = ow.TryMove(state, CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.EnterDungeon, result.Kind);
        Assert.Equal(1, result.DungeonId);
        Assert.Equal(20, result.DestX);
        Assert.Equal(0, result.DestY);

        var dng = data.CreateDungeonTraversal();
        var ds = new DungeonState();
        dng.Enter(ds, result.DungeonId!.Value, result.DestX!.Value, result.DestY!.Value);
        Assert.Equal(0xd7, dng.GetLevel(1).Get(ds.X, ds.Y));
    }
}
