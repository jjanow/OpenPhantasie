namespace OpenPhantasie.Core;

/// <summary>
/// What IV's location-map view shows (<c>_idcopy</c>, <c>_calim</c>, <c>_idraw</c>; <c>FORMAT.md</c> §6.9 "The location-map
/// view"): a 9 × 9 window of <c>_pinmap</c> words around the party, which of them the party sees, and the party marker.
/// The view is overhead with walls standing up (manual: "from diagonally above"), not first-person. Headless: the Client
/// draws the pictures (floor = bits 0–5, north wall = bits 6–10, west wall = bits 11–13, edge marks = bits 14 / 15).
/// </summary>
public sealed class P4DungeonView
{
    public const int Size = 9, Centre = 4;

    /// <summary>A cell outside the map (<c>_idcopy</c>: 0xFFFF).</summary>
    public const int Outside = 0xFFFF;

    /// <summary>North picture 31 / west picture 7: no wall on that side.</summary>
    public const int NoNorth = 31, NoWest = 7;

    /// <summary>North picture 7 (the grate): <c>_calim</c> lets sight pass it northward only.</summary>
    public const int Grate = 7;

    /// <summary>Bit 14: the south side is a doorway's near edge; bit 15: the east side (<c>_prex</c>).</summary>
    public const int SouthMark = 0x4000, EastMark = 0x8000;

    private P4DungeonView(int[] cells, bool[] seen, int marker)
    {
        Cells = cells;
        Seen = seen;
        Marker = marker;
    }

    /// <summary><c>_pibuf</c>: 9 × 9 cells, row after row (index = 9 · row + column), each a word with bits 14 / 15 or <see cref="Outside"/>.</summary>
    public IReadOnlyList<int> Cells { get; }

    /// <summary><c>_lookf</c>: whether the party sees each cell (unseen cells are drawn black, without walls).</summary>
    public IReadOnlyList<bool> Seen { get; }

    /// <summary>The <c>mn.pix</c> picture of the party: the facing 0–3, + 4 on water floor 27–30 without Levitation.</summary>
    public int Marker { get; }

    public int Cell(int column, int row) => Cells[row * Size + column];

    public bool IsSeen(int column, int row) => Seen[row * Size + column];

    public static int Floor(int cell) => cell & 0x3f;

    public static int North(int cell) => (cell >> 6) & 0x1f;

    public static int West(int cell) => (cell >> 11) & 7;

    /// <summary>The view around (<paramref name="x"/>, <paramref name="y"/>) facing <paramref name="dir"/> (0 N … 3 W).</summary>
    public static P4DungeonView Of(P4DungeonMap map, int x, int y, int dir, bool levitating)
    {
        ArgumentNullException.ThrowIfNull(map);
        var cells = new int[Size * Size];
        for (var r = 0; r < Size; r++)
            for (var c = 0; c < Size; c++)
            {
                int mx = x - Centre + c, my = y - Centre + r;
                cells[r * Size + c] = mx is < 0 or >= P4DungeonFile.Size || my is < 0 or >= P4DungeonFile.Size
                    ? Outside
                    : (map.Word(mx, my) & 0x3fff) | map.EdgeMarks[my * P4DungeonFile.Size + mx];
            }
        var floor = Floor(cells[Centre * Size + Centre]);
        var marker = (dir & 3) + (floor is >= 27 and <= 30 && !levitating ? 4 : 0);
        return new P4DungeonView(cells, Calim(cells), marker);
    }

    /// <summary>
    /// <c>_prex</c> over a 31 × 31 <c>_pinmap</c>: for each square of 30 × 30, bit 14 when the north picture of the square
    /// below is 2, 3 or 16–19, bit 15 when the west picture of the square to the right is 0 or 5.
    /// </summary>
    public static ushort[] Prex(IReadOnlyList<ushort> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        const int s = P4DungeonFile.Stride, n = P4DungeonFile.Size;
        var marks = new ushort[n * n];
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var south = (words[(y + 1) * s + x] >> 6) & 0x1f;
                var east = (words[y * s + x + 1] >> 11) & 7;
                var m = 0;
                if (south is 2 or 3 or >= 16 and <= 19)
                    m |= SouthMark;
                if (east is 0 or 5)
                    m |= EastMark;
                marks[y * n + x] = (ushort)m;
            }
        return marks;
    }

    /// <summary>
    /// <c>_calim</c> on a 9 × 9 window: the centre; arms west and east along row 4 (a cell is entered when the west wall
    /// between is 7), each entered cell looking north (while row &gt; 0 and its north picture is 31, or it is 7) and south
    /// (while the entered cell's north picture is 31); then the same along column 4, each entered cell looking west / east.
    /// The code steps on to row −1 past a grate in the top row and writes outside the table (a slip); the remaster stops
    /// at the window's edge <c>[~]</c>.
    /// </summary>
    public static bool[] Calim(IReadOnlyList<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var seen = new bool[Size * Size];
        int Nw(int c, int r) => North(cells[r * Size + c]);
        int Ww(int c, int r) => West(cells[r * Size + c]);
        void Mark(int c, int r) => seen[r * Size + c] = true;

        void LookNorthSouth(int c)
        {
            for (var r = Centre; r > 0 && Nw(c, r) == NoNorth || r > 0 && Nw(c, r) == Grate; r--)
                Mark(c, r - 1);
            for (var r = Centre + 1; r < Size && Nw(c, r) == NoNorth; r++)
                Mark(c, r);
        }

        void LookWestEast(int r)
        {
            for (var c = Centre; c > 0 && Ww(c, r) == NoWest; c--)
                Mark(c - 1, r);
            for (var c = Centre + 1; c < Size && Ww(c, r) == NoWest; c++)
                Mark(c, r);
        }

        Mark(Centre, Centre);
        for (var c = Centre; c > 0 && Ww(c, Centre) == NoWest; c--)
        {
            Mark(c - 1, Centre);
            LookNorthSouth(c - 1);
        }
        for (var c = Centre + 1; c < Size && Ww(c, Centre) == NoWest; c++)
        {
            Mark(c, Centre);
            LookNorthSouth(c);
        }
        for (var r = Centre; r > 0 && Nw(Centre, r) == NoNorth || r > 0 && Nw(Centre, r) == Grate; r--)
        {
            Mark(Centre, r - 1);
            LookWestEast(r - 1);
        }
        for (var r = Centre + 1; r < Size && Nw(Centre, r) == NoNorth; r++)
        {
            Mark(Centre, r);
            LookWestEast(r);
        }
        return seen;
    }
}
