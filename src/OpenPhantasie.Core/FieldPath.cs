namespace OpenPhantasie.Core;

/// <summary>
/// Shortest four-way path on the wilderness or dungeon grid for mouse walking (presentation QoL, no rule change: every
/// step still goes through the normal step code and the walk stops on anything but a plain move). The predicates only
/// read what the player can see, so a path never leaks fogged or unvisited cells, nor hidden traps or events.
/// </summary>
public static class FieldPath
{
    // Breadth-first order; the first shortest path found wins, so ties go north, east, south, west.
    private static readonly (CardinalDirection Dir, int Dx, int Dy)[] Steps =
    [
        (CardinalDirection.North, 0, -1),
        (CardinalDirection.East, 1, 0),
        (CardinalDirection.South, 0, 1),
        (CardinalDirection.West, -1, 0),
    ];

    /// <summary>Screen delta of a step (+y is south, as on both maps).</summary>
    public static (int Dx, int Dy) Delta(CardinalDirection dir) => dir switch
    {
        CardinalDirection.North => (0, -1),
        CardinalDirection.South => (0, 1),
        CardinalDirection.East => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>
    /// The steps from <paramref name="from"/> to <paramref name="to"/> on a <paramref name="width"/> × <paramref name="height"/>
    /// grid, entering only cells where <paramref name="canEnter"/> is true (the goal included; the start is not checked).
    /// Empty when already there; null when there is no way.
    /// </summary>
    public static IReadOnlyList<CardinalDirection>? Find(
        int width, int height, (int X, int Y) from, (int X, int Y) to, Func<int, int, bool> canEnter)
    {
        ArgumentNullException.ThrowIfNull(canEnter);
        bool Inside(int x, int y) => (uint)x < (uint)width && (uint)y < (uint)height;
        if (!Inside(from.X, from.Y) || !Inside(to.X, to.Y))
            return null;
        if (from == to)
            return [];

        var cameBy = new int[width * height]; // step index + 1; 0 = not reached
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(from);
        cameBy[from.Y * width + from.X] = -1;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            for (var i = 0; i < Steps.Length; i++)
            {
                var (_, dx, dy) = Steps[i];
                var nx = x + dx;
                var ny = y + dy;
                if (!Inside(nx, ny) || cameBy[ny * width + nx] != 0 || !canEnter(nx, ny))
                    continue;
                cameBy[ny * width + nx] = i + 1;
                if ((nx, ny) == to)
                    return Trace(width, cameBy, from, to);
                queue.Enqueue((nx, ny));
            }
        }
        return null;
    }

    private static List<CardinalDirection> Trace(int width, int[] cameBy, (int X, int Y) from, (int X, int Y) to)
    {
        var path = new List<CardinalDirection>();
        var (x, y) = to;
        while ((x, y) != from)
        {
            var (dir, dx, dy) = Steps[cameBy[y * width + x] - 1];
            path.Add(dir);
            x -= dx;
            y -= dy;
        }
        path.Reverse();
        return path;
    }

    /// <summary>A wilderness cell the player can see: not under fog (raw tile below <see cref="OverworldRegion.FogOffset"/>).</summary>
    public static bool OverworldSeen(OverworldRegion region, int x, int y) =>
        region.GetRaw(x, y) < OverworldRegion.FogOffset;

    /// <summary>
    /// A wilderness cell a mouse walk may step on: seen and not a wall. Cells on the way (not the goal) must also be plain
    /// land — no water (class 10), town (40), inn (41), dungeon (class 5) or special site (class 11) — as stepping on
    /// those stops the walk anyway.
    /// </summary>
    public static bool OverworldCanEnter(OverworldRegion region, int x, int y, bool goal)
    {
        if (!OverworldSeen(region, x, y))
            return false;
        var tile = region.GetNormalized(x, y);
        if (tile == OverworldRegion.WallTile)
            return false;
        return goal || (tile != 40 && tile != 41 && tile / 10 is not (5 or 10 or 11));
    }

    /// <summary>
    /// Mouse walk path on the current wilderness region (stays inside it); null when the goal is fogged, a wall or out of reach.
    /// </summary>
    public static IReadOnlyList<CardinalDirection>? Overworld(OverworldRegion region, (int X, int Y) from, (int X, int Y) to)
    {
        if (!region.InBounds(to.X, to.Y))
            return null;
        return Find(region.GridWidth, region.GridHeight, from, to,
                   (x, y) => OverworldCanEnter(region, x, y, (x, y) == to))
               // No plain-land way: allow any seen non-wall cell on the way (the walk stops where the step isn't plain).
               ?? Find(region.GridWidth, region.GridHeight, from, to, (x, y) => OverworldCanEnter(region, x, y, goal: true));
    }

    /// <summary>
    /// Mouse walk path on a dungeon level over the cells drawn on the map (<paramref name="seen"/>) that are not walls. Traps,
    /// events and fights are not avoided: they look like floor, and the walk stops on them like a key press would.
    /// A <see cref="CorridorCell"/> (<c>0x01</c>) is drawn with the wall glyph (OV2 <c>0x21df</c>), so it joins a route
    /// (goal included) only once the party has stood on it (<paramref name="stoodOn"/>; user s77): the map shows a wall
    /// there until then, and a walk must not give the passage away. The arrow keys walk it as before.
    /// </summary>
    public static IReadOnlyList<CardinalDirection>? Dungeon(
        DungeonLevel level, (int X, int Y) from, (int X, int Y) to, Func<int, int, bool> seen, Func<int, int, bool> stoodOn)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(stoodOn);
        return Find(level.GridWidth, level.GridHeight, from, to,
            (x, y) => seen(x, y) && level.IsWalkable(x, y) && (level.Get(x, y) != CorridorCell || stoodOn(x, y)));
    }

    /// <summary>The <c>0x01</c> cell: walked like floor, drawn like a wall (OV2 <c>0x21df</c>).</summary>
    public const byte CorridorCell = 0x01;
}
