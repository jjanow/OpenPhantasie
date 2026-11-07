using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class OverworldTraversalTests
{
    private static OverworldTraversal Traversal =>
        GameDataRepository.LoadPhantasie1().CreateTraversal();

    [OriginalDataFact]
    public void NewGameSpawn_IsRegion1_At_20_5()
    {
        var state = new OverworldState();
        Assert.Equal(1, state.RegionId);
        Assert.Equal(20, state.X);
        Assert.Equal(5, state.Y);

        var region = Traversal.GetRegion(1);
        Assert.Equal(16, region.GetNormalized(20, 5)); // path/feature tile
    }

    [OriginalDataFact]
    public void Appleton_IsATownTile_D060()
    {
        // map-patches.json: DOS has inn tile 41 at OUT13 (17,19), where APPLETON's town record points (user s53: manual).
        var traversal = Traversal;
        Assert.Equal(40, traversal.GetRegion(13).GetNormalized(17, 19));
        var state = new OverworldState { RegionId = 13, X = 17, Y = 18 };
        var result = traversal.TryMove(state, CardinalDirection.South);
        Assert.Equal(OverworldMoveKind.EnterTown, result.Kind);
        Assert.Equal(5, result.TownId);
    }

    [OriginalDataFact]
    public void AppletonInn_MovedBesideTheDungeon_D060()
    {
        // map-patches.json: the inn displaced from APPLETON's cell sits at OUT13 (4,14), next to the dungeon at (3,14).
        var traversal = Traversal;
        Assert.Equal(41, traversal.GetRegion(13).GetNormalized(4, 14));
        var state = new OverworldState { RegionId = 13, X = 4, Y = 15 };
        Assert.Equal(OverworldMoveKind.WildernessInn, traversal.TryMove(state, CardinalDirection.North).Kind);
    }

    [OriginalDataFact]
    public void MoveOntoTownTile_ReturnsEnterTown()
    {
        var traversal = Traversal;
        var state = new OverworldState { RegionId = 1, X = 14, Y = 15 };
        // Stand on town already — step away then back if needed.
        // Feature at (14,15) is town arg=1; move from (13,15) east onto it.
        state.X = 13;
        state.Y = 15;
        var result = traversal.TryMove(state, CardinalDirection.East);
        Assert.Equal(OverworldMoveKind.EnterTown, result.Kind);
        Assert.Equal(14, result.X);
        Assert.Equal(15, result.Y);
        Assert.Equal(1, result.TownId);
        Assert.Equal(40, result.Tile);
    }

    [OriginalDataFact]
    public void MoveOntoDungeonTile_ReturnsEnterDungeon()
    {
        var traversal = Traversal;
        var state = new OverworldState { RegionId = 1, X = 8, Y = 8 };
        var result = traversal.TryMove(state, CardinalDirection.East); // (9,8) is dungeon 50
        Assert.Equal(OverworldMoveKind.EnterDungeon, result.Kind);
        Assert.Equal(9, result.X);
        Assert.Equal(8, result.Y);
        Assert.Equal(50, result.Tile);
        Assert.Equal(1, result.DungeonId);
        Assert.Equal(20, result.DestX);
    }

    [OriginalDataFact]
    public void WallTile_BlocksMovement()
    {
        var traversal = Traversal;
        var region = traversal.GetRegion(1);
        // Find a walkable cell next to a wall (100).
        for (var y = 0; y < OverworldRegion.Height; y++)
        for (var x = 0; x < OverworldRegion.Width - 1; x++)
        {
            if (region.GetNormalized(x, y) == OverworldRegion.WallTile)
                continue;
            if (region.GetNormalized(x + 1, y) != OverworldRegion.WallTile)
                continue;

            var state = new OverworldState { RegionId = 1, X = x, Y = y };
            var result = traversal.TryMove(state, CardinalDirection.East);
            Assert.Equal(OverworldMoveKind.Blocked, result.Kind);
            Assert.Equal(x, state.X);
            Assert.Equal(y, state.Y);
            return;
        }

        Assert.Fail("No wall-adjacent walkable tile found in OUT1.");
    }

    [OriginalDataFact]
    public void CrossingWestEdge_EntersRegion5()
    {
        var traversal = Traversal;
        // OUT1 west edge → OUT5, arrive x=25.
        // Find a non-wall tile on x=0 of OUT1.
        var region = traversal.GetRegion(1);
        for (var y = 0; y < OverworldRegion.Height; y++)
        {
            if (region.GetNormalized(0, y) == OverworldRegion.WallTile)
                continue;
            var dest = traversal.GetRegion(5);
            if (dest.GetNormalized(25, y) == OverworldRegion.WallTile)
                continue;

            var state = new OverworldState { RegionId = 1, X = 0, Y = y };
            var result = traversal.TryMove(state, CardinalDirection.West);
            Assert.NotEqual(OverworldMoveKind.Blocked, result.Kind);
            Assert.Equal(5, state.RegionId);
            Assert.Equal(25, state.X);
            Assert.Equal(y, state.Y);
            return;
        }

        Assert.Fail("No usable west-edge crossing found.");
    }

    [Fact]
    public void AdjacentRegionFormula_MatchesOv2()
    {
        Assert.Equal(5, OverworldTraversal.AdjacentRegion(1, -1, 0)); // West
        Assert.Equal(-3, OverworldTraversal.AdjacentRegion(1, 1, 0));  // East (invalid)
        Assert.Equal(0, OverworldTraversal.AdjacentRegion(1, 0, 1));   // North (invalid)
        Assert.Equal(2, OverworldTraversal.AdjacentRegion(1, 0, -1));  // South
    }

    [OriginalDataFact]
    public void RandomEncounter_TriggersOnRollUnder6()
    {
        var traversal = Traversal;
        var state = new OverworldState { RegionId = 1, X = 20, Y = 5 };
        // Move south onto ordinary terrain with forced encounter roll.
        var forced = new FixedRollRandom(0); // roll 0 < 6
        var result = traversal.TryMove(state, CardinalDirection.South, forced);
        if (result.Kind is OverworldMoveKind.LocationText or OverworldMoveKind.EnterTown
            or OverworldMoveKind.EnterDungeon or OverworldMoveKind.SpecialSite
            or OverworldMoveKind.ScriptedEncounter or OverworldMoveKind.Blocked)
        {
            // Spawn neighborhood may not be "ordinary"; try a known clear path tile.
            state = FindOrdinaryStep(traversal);
            result = traversal.TryMove(state, CardinalDirection.East, forced);
        }

        Assert.Equal(OverworldMoveKind.RandomEncounter, result.Kind);
    }

    [OriginalDataFact]
    public void FogReveal_ClearsPlus120Encoding()
    {
        var region = Traversal.GetRegion(1);
        region.Tiles[0] = (byte)(10 + OverworldRegion.FogOffset);
        Assert.Equal(10, region.GetNormalized(0, 0));
        region.Reveal(0, 0);
        Assert.Equal(10, region.GetRaw(0, 0));
    }

    private static OverworldState FindOrdinaryStep(OverworldTraversal traversal)
    {
        var region = traversal.GetRegion(1);
        for (var y = 0; y < OverworldRegion.Height; y++)
        for (var x = 0; x < OverworldRegion.Width - 1; x++)
        {
            var here = region.GetNormalized(x, y);
            var east = region.GetNormalized(x + 1, y);
            if (here == OverworldRegion.WallTile || east == OverworldRegion.WallTile)
                continue;
            if (east is 40 or 41 || east / 10 is 5 or 10 or 11 || east % 10 is 6 or 7)
                continue;
            return new OverworldState { RegionId = 1, X = x, Y = y };
        }

        throw new InvalidOperationException("No ordinary step found.");
    }

    private sealed class FixedRollRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }
}
