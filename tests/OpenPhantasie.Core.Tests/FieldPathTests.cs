using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>Mouse walking (presentation QoL): four-way shortest paths over what the player can see only.</summary>
public class FieldPathTests
{
    private static (int X, int Y) Walk((int X, int Y) from, IEnumerable<CardinalDirection> path)
    {
        foreach (var dir in path)
        {
            var (dx, dy) = FieldPath.Delta(dir);
            from = (from.X + dx, from.Y + dy);
        }
        return from;
    }

    [Fact]
    public void Find_OpenGrid_IsShortestAndEndsOnTheGoal()
    {
        var path = FieldPath.Find(10, 10, (1, 1), (4, 3), (_, _) => true)!;
        Assert.Equal(5, path.Count);
        Assert.Equal((4, 3), Walk((1, 1), path));
    }

    [Fact]
    public void Find_SameCell_IsEmpty_OutOfGrid_IsNull()
    {
        Assert.Empty(FieldPath.Find(5, 5, (2, 2), (2, 2), (_, _) => true)!);
        Assert.Null(FieldPath.Find(5, 5, (2, 2), (5, 2), (_, _) => true));
    }

    [Fact]
    public void Find_GoesAroundAWall_AndGivesUpWhenSealed()
    {
        // A wall on column 2 except row 4.
        bool Open(int x, int y) => x != 2 || y == 4;
        var path = FieldPath.Find(5, 5, (0, 0), (4, 0), Open)!;
        Assert.Equal((4, 0), Walk((0, 0), path));
        Assert.Equal(12, path.Count); // down 4, across 4, up 4

        Assert.Null(FieldPath.Find(5, 5, (0, 0), (4, 0), (x, _) => x != 2));
    }

    [Fact]
    public void Find_NeverEntersACellThePredicateRefuses()
    {
        var entered = new HashSet<(int, int)>();
        static bool Open(int x, int y) => !(x == 2 && y < 5) && !(x == 4 && y > 0);
        var path = FieldPath.Find(6, 6, (0, 0), (5, 5), Open)!;
        var at = (X: 0, Y: 0);
        foreach (var dir in path)
        {
            var (dx, dy) = FieldPath.Delta(dir);
            at = (at.X + dx, at.Y + dy);
            entered.Add(at);
        }
        Assert.All(entered, c => Assert.True(Open(c.Item1, c.Item2)));
        Assert.Contains((5, 5), entered);
    }

    private static OverworldRegion Region(Func<int, int, byte> tile)
    {
        var tiles = new byte[OverworldRegion.Width * OverworldRegion.Height];
        for (var y = 0; y < OverworldRegion.Height; y++)
        for (var x = 0; x < OverworldRegion.Width; x++)
            tiles[y * OverworldRegion.Width + x] = tile(x, y);
        return new OverworldRegion(1, "TEST", tiles, [], []);
    }

    [Fact]
    public void Overworld_DoesNotPathThroughFog()
    {
        // Row 5 is fogged except at x = 20; the only seen way round is through that gap.
        var region = Region((x, y) => (byte)(y == 5 && x != 20 ? 120 : 0));
        var path = FieldPath.Overworld(region, (3, 3), (3, 8))!;
        var at = (X: 3, Y: 3);
        foreach (var dir in path)
        {
            var (dx, dy) = FieldPath.Delta(dir);
            at = (at.X + dx, at.Y + dy);
            Assert.True(FieldPath.OverworldSeen(region, at.X, at.Y));
        }
        Assert.Equal((3, 8), at);

        // A fogged goal can't be walked to.
        Assert.Null(FieldPath.Overworld(region, (3, 3), (4, 5)));
    }

    [Fact]
    public void Overworld_WallGoal_IsNull_TownGoal_IsAllowed_TownOnTheWay_IsAvoided()
    {
        var region = Region((x, y) => (byte)(x == 5 && y == 2 ? OverworldRegion.WallTile : x == 5 && y == 4 ? 40 : 0));
        Assert.Null(FieldPath.Overworld(region, (2, 2), (5, 2)));
        Assert.NotNull(FieldPath.Overworld(region, (2, 4), (5, 4)));

        // Straight east along row 4 would cross the town at x = 5: the path goes round it.
        var path = FieldPath.Overworld(region, (2, 4), (8, 4))!;
        Assert.Equal(8, path.Count);
        var at = (X: 2, Y: 4);
        foreach (var dir in path)
        {
            var (dx, dy) = FieldPath.Delta(dir);
            at = (at.X + dx, at.Y + dy);
            Assert.NotEqual((5, 4), at);
        }
    }

    [Fact]
    public void Overworld_WaterOnlyWay_StillWalks_ThroughIt()
    {
        // Columns 10..12 are water (class 10) from top to bottom: no dry way, so the walk may swim (and stops there).
        var region = Region((x, _) => (byte)(x is >= 10 and <= 12 ? 102 : 0));
        var path = FieldPath.Overworld(region, (8, 3), (14, 3));
        Assert.NotNull(path);
        Assert.Equal(6, path!.Count);
    }

    [Fact]
    public void Dungeon_UsesOnlySeenWalkableCells()
    {
        var cells = new byte[DungeonLevel.GridByteLength];
        for (var x = 1; x <= 8; x++)
            cells[3 * DungeonLevel.Width + x] = 1; // a corridor on row 3
        var level = new DungeonLevel(1, "TEST", cells);

        var path = FieldPath.Dungeon(level, (1, 3), (8, 3), (_, _) => true, (_, _) => true)!;
        Assert.Equal(7, path.Count);
        Assert.All(path, d => Assert.Equal(CardinalDirection.East, d));

        // Cell (5,3) not drawn yet: no path, even though the corridor is open.
        Assert.Null(FieldPath.Dungeon(level, (1, 3), (8, 3), (x, y) => (x, y) != (5, 3), (_, _) => true));
        // A wall goal.
        Assert.Null(FieldPath.Dungeon(level, (1, 3), (1, 4), (_, _) => true, (_, _) => true));
    }

    [Fact]
    public void Dungeon_WallGlyphCell_JoinsARoute_OnlyOnceStoodOn()
    {
        // Row 3 is floor (7) except (5,3), a 0x01 cell; row 5 is a floor detour joined at (2,4) and (7,4).
        var cells = new byte[DungeonLevel.GridByteLength];
        for (var x = 1; x <= 8; x++)
        {
            cells[3 * DungeonLevel.Width + x] = 7;
            cells[5 * DungeonLevel.Width + x] = 7;
        }
        cells[3 * DungeonLevel.Width + 5] = FieldPath.CorridorCell;
        cells[4 * DungeonLevel.Width + 2] = 7;
        cells[4 * DungeonLevel.Width + 7] = 7;
        var level = new DungeonLevel(1, "TEST", cells);
        bool Seen(int x, int y) => true;

        // Not stood on: the walk takes the detour (2 down, 5 east, 2 up from (2,3)), never (5,3).
        var detour = FieldPath.Dungeon(level, (1, 3), (8, 3), Seen, (_, _) => false)!;
        Assert.Equal(11, detour.Count);
        Assert.Null(FieldPath.Dungeon(level, (1, 3), (5, 3), Seen, (_, _) => false)); // nor as the goal

        // Stood on: straight through.
        var straight = FieldPath.Dungeon(level, (1, 3), (8, 3), Seen, (x, y) => (x, y) == (5, 3))!;
        Assert.Equal(Enumerable.Repeat(CardinalDirection.East, 7), straight);

        // Plain floor needs no stood-on mark, and the start cell is never checked (the party stands on (5,3)).
        Assert.Equal(Enumerable.Repeat(CardinalDirection.East, 3), FieldPath.Dungeon(level, (5, 3), (8, 3), Seen, (_, _) => false));
    }
}
