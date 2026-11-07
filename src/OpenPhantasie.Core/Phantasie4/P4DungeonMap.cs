namespace OpenPhantasie.Core;

/// <summary>One trigger entry of an <c>O_0xx</c> event file: square, facing (4 = any) and code address (base-relative).</summary>
public sealed record P4EventTrigger(int X, int Y, int Dir, int Address)
{
    /// <summary>x = 0x20: runs when the map is loaded; x = 0x21: the party walked off the map edge (<c>events.md</c> §2.1).</summary>
    public const int OnLoad = 0x20, OnEdge = 0x21, AnyDirection = 4;
}

/// <summary>
/// One Phantasie IV location map n (1–21) as shipped: <c>IND&lt;n&gt;.PDT</c> (31 × 31 words, <c>_pinmap</c>) and its
/// <c>O_0nn</c> event file (trigger entries, then code and text from the code base; all addresses base-relative,
/// operands little-endian). <c>events.md</c> §1–2.
/// </summary>
public sealed record P4DungeonFile(int Map, string Name, IReadOnlyList<ushort> Words, IReadOnlyList<P4EventTrigger> Triggers,
    IReadOnlyList<byte> Code)
{
    public const int Stride = 31, Size = 30;

    /// <summary>The string id of the event text at base-relative address <paramref name="address"/> (<c>o&lt;map&gt;.&lt;addr&gt;</c>).</summary>
    public string TextId(int address) => $"o{Map:00}.{address}";
}

/// <summary>
/// A loaded location map (<c>_dload</c>): the live copy of <c>_pinmap</c> that events change (floors, wall pictures) and the
/// movement map <c>_dinmap</c> (<c>_mkdin</c>): four 4-bit side codes per square, N bits 0–3, E 4–7, S 8–11, W 12–15;
/// 0 open, 1 wall, 2 locked door, 3 door that will not open, 7 map edge. Both are rebuilt from the file on every load.
/// </summary>
public sealed class P4DungeonMap
{
    public const int Open = 0, Wall = 1, LockedDoor = 2, SealedDoor = 3, Edge = 7;

    /// <summary>Floor type 6: a chest already opened (<c>_dchest</c>).</summary>
    public const int OpenedChest = 6;

    /// <summary>The wall pictures that leave a north / south side open (<c>_mkdin</c>): 2, 3, 16 (gate), 19 (open gate), 31.</summary>
    private static readonly int[] OpenNorthPictures = [31, 2, 3, 16, 19];

    /// <summary>The west-wall values (3 bits) that leave a west / east side open: 7, 0, 5.</summary>
    private static readonly int[] OpenWestPictures = [7, 0, 5];

    /// <summary>
    /// The movement code of the floor beyond an open side (<c>0x437E6</c>, int32 per floor type 0–43): 1 for 4, 7–26,
    /// 33–39 and 41, else 0. The maps use floor types 0–43 only.
    /// </summary>
    private static readonly int[] FloorBlocks =
    [
        0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1,
        0, 1, 0, 0,
    ];

    public P4DungeonFile File { get; }

    public int Map => File.Map;

    /// <summary><c>_pinmap</c>, 31 × 31 (row / column 30 hold the south / east walls of the last row / column).</summary>
    public ushort[] Words { get; }

    /// <summary><c>_dinmap</c>, 30 × 30.</summary>
    public ushort[] Moves { get; }

    public P4DungeonMap(P4DungeonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Words.Count != P4DungeonFile.Stride * P4DungeonFile.Stride)
            throw new ArgumentException($"Map {file.Map}: {file.Words.Count} words, expected 961.", nameof(file));
        File = file;
        Words = [.. file.Words];
        Moves = new ushort[P4DungeonFile.Size * P4DungeonFile.Size];
        BuildMoves();
        EdgeMarks = P4DungeonView.Prex(Words);
    }

    /// <summary>A restored map (a save under <c>p4.dungeon-reload</c> = <c>saved-map</c>): the live words and movement codes as they were.</summary>
    public P4DungeonMap(P4DungeonFile file, ushort[] words, ushort[] moves)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        Words = words;
        Moves = moves;
        EdgeMarks = P4DungeonView.Prex(Words);
    }

    /// <summary>
    /// <c>_prex</c>'s bits 14 / 15 of each square (30 × 30, values 0x4000 / 0x8000): the near edges of doorways the view
    /// draws (<see cref="P4DungeonView"/>). Computed when the map is loaded only, as <c>_dload</c> does; events that change
    /// pictures afterwards do not redo them. A restored map (<c>p4.dungeon-reload</c> = <c>saved-map</c>) computes them from
    /// its saved words <c>[~]</c>; the default reload builds the map fresh, as the original does.
    /// </summary>
    public ushort[] EdgeMarks { get; }

    public ushort Word(int x, int y) => Words[y * P4DungeonFile.Stride + x];

    /// <summary>Bits 0–5 of the square's word.</summary>
    public int Floor(int x, int y) => Word(x, y) & 0x3f;

    /// <summary>Bits 6–10: the north-side wall picture.</summary>
    public int NorthPicture(int x, int y) => (Word(x, y) >> 6) & 0x1f;

    /// <summary>Bits 11–13: the west-side wall.</summary>
    public int WestPicture(int x, int y) => (Word(x, y) >> 11) & 7;

    public int Move(int x, int y, int dir) => (Moves[y * P4DungeonFile.Size + x] >> (4 * (dir & 0xf))) & 0xf;

    /// <summary><c>_setmove</c>: side <paramref name="dir"/> of (x, y) gets <paramref name="code"/>.</summary>
    public void SetMove(int x, int y, int dir, int code)
    {
        var shift = 4 * (dir & 0xf);
        ref var w = ref Moves[y * P4DungeonFile.Size + x];
        w = (ushort)((code << shift) | (w & ~(0xf << shift) & 0xffff));
    }

    /// <summary><c>_setfl</c> (SETFLOOR): the floor bits only; the movement map is not rebuilt.</summary>
    public void SetFloor(int x, int y, int floor)
    {
        ref var w = ref Words[y * P4DungeonFile.Stride + x];
        w = (ushort)((w & 0xffc0) | (floor & 0x3f));
    }

    /// <summary><c>_setyoko</c> (WALLPIC5 / WALLPIC4): the north wall picture only, graphics; movement untouched.</summary>
    public void SetNorthPicture(int x, int y, int picture)
    {
        ref var w = ref Words[y * P4DungeonFile.Stride + x];
        w = (ushort)((w & ~(0x1f << 6) & 0xffff) | ((picture & 0x1f) << 6));
    }

    /// <summary>
    /// <c>_openg</c> (OPENGATE): a north picture 16 (gate) is animated in three +0x40 steps to 19 (open gate). Any other
    /// picture is left alone. Movement codes are not touched (scripts follow with WALL0). True when it opened.
    /// </summary>
    public bool OpenGate(int x, int y)
    {
        if (NorthPicture(x, y) != 16)
            return false;
        Words[y * P4DungeonFile.Stride + x] += 3 * 0x40;
        return true;
    }

    /// <summary>
    /// <c>_mkdin</c> (<c>0x1A440</c>): for each square, N from its own north picture (y = 0 open → edge 7), W from its own
    /// west wall (x = 0 → 7), S from the north picture of the square below (y = 29 → 7), E from the west wall of the square
    /// to the right (x = 29 → 7); an open side then takes the code of the neighbour's floor type.
    /// </summary>
    private void BuildMoves()
    {
        const int last = P4DungeonFile.Size - 1;
        for (var y = 0; y < P4DungeonFile.Size; y++)
        for (var x = 0; x < P4DungeonFile.Size; x++)
        {
            var n = Side(OpenNorthPictures.Contains(NorthPicture(x, y)), y == 0, x, y - 1);
            var w = Side(OpenWestPictures.Contains(WestPicture(x, y)), x == 0, x - 1, y);
            var s = Side(OpenNorthPictures.Contains(NorthPicture(x, y + 1)), y == last, x, y + 1);
            var e = Side(OpenWestPictures.Contains(WestPicture(x + 1, y)), x == last, x + 1, y);
            Moves[y * P4DungeonFile.Size + x] = (ushort)((w << 12) | (s << 8) | (e << 4) | n);
        }
    }

    private int Side(bool open, bool atEdge, int nx, int ny)
    {
        if (!open)
            return Wall;
        if (atEdge)
            return Edge;
        var floor = Floor(nx, ny);
        return floor < FloorBlocks.Length ? FloorBlocks[floor] : Wall;
    }

    /// <summary>Floor types 27–30 are water (<c>_dswim</c>, <c>_denc</c>).</summary>
    public static bool IsWater(int floor) => floor is > 26 and < 31;

    /// <summary>
    /// <c>_denc</c>'s test: the floor masked to 5 bits (an original slip; the maps' floors 0–43 make it the same as
    /// <see cref="IsWater"/>) — no fight starts on water.
    /// </summary>
    public bool FightRefused(int x, int y) => IsWater(Word(x, y) & 0x1f);

    /// <summary>
    /// <c>_denc</c>'s battle terrain: 0 when the 5-bit floor is 3 or 31 (so floors 3, 31 and 35; the code's test for 32 can
    /// never match), else 1.
    /// </summary>
    public int BattleTerrain(int x, int y) => (Word(x, y) & 0x1f) is 3 or 31 ? 0 : 1;
}
