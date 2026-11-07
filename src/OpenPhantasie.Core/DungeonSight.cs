namespace OpenPhantasie.Core;

/// <summary>
/// What the dungeon map shows: the step reveal OV2 <c>0x20e5</c> and L)OOK <c>0x1f9f</c> (dungeon-representation.md
/// §Sight [C], s67). DOS keeps no seen bits in the level; the drawn screen image is the only record, so these return the
/// cells drawn and the caller keeps them.
/// </summary>
public static class DungeonSight
{
    /// <summary>How far a Look ray goes (<c>0x1f9f</c>: k = 1..3).</summary>
    public const int LookRayLength = 3;

    /// <summary>
    /// Cells a step onto (<paramref name="x"/>, <paramref name="y"/>) draws (<c>0x20e5</c>): the 3×3 box around it,
    /// clipped to the level; only the cell itself on <c>0x00</c>/<c>0x01</c> and lock cells <c>0xb9..0xc7</c>.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> StepReveal(DungeonLevel level, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(level);
        var c = level.Get(x, y);
        var r = c < 2 || c is >= 0xb9 and <= 0xc7 ? 0 : 1;
        var cells = new List<(int, int)>();
        for (var b = y - r; b <= y + r; b++)
        for (var a = x - r; a <= x + r; a++)
            if (level.InBounds(a, b))
                cells.Add((a, b));
        return cells;
    }

    /// <summary>
    /// L)OOK from (<paramref name="x"/>, <paramref name="y"/>) (<c>0x1f9f</c>): 9 rays (8 directions and the party's
    /// cell) of up to <see cref="LookRayLength"/> cells, each stopping before the first cell outside <c>0x02..0xb8</c>
    /// or the level; the step box around each ray's farthest cell is drawn (open floor: the 9×9 square). Diagonal rays
    /// have no corner check. Null where DOS silently does nothing: the party on <c>0x00</c>/<c>0x01</c> or a lock/door
    /// cell <c>0xb9..0xd6</c>. Costs nothing: no time, roll or cell event.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)>? Look(DungeonLevel level, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(level);
        var c = level.Get(x, y);
        if (c < 2 || c is >= 0xb9 and <= 0xd6)
            return null;
        var cells = new HashSet<(int, int)>();
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var m = 0;
            for (var k = 1; k <= LookRayLength; k++)
            {
                int cx = x + k * dx, cy = y + k * dy;
                if (!level.InBounds(cx, cy) || level.Get(cx, cy) is < 2 or > 0xb8)
                    break;
                m = k;
            }
            cells.UnionWith(StepReveal(level, x + m * dx, y + m * dy));
        }
        return cells.OrderBy(p => p.Item2).ThenBy(p => p.Item1).ToList();
    }
}
