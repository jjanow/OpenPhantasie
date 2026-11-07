namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Pillow 12.1's filled polygon on an 8-bit image (<c>ImageDraw.polygon(xy, fill=…)</c>: <c>ImagingDrawPolygon</c> +
/// <c>polygon_generic</c> + <c>hline8</c> in <c>libImaging/Draw.c</c>), ported line for line, single-precision maths
/// included, because the Amiga map pictures (<c>extract_amiga_map_graphics.py</c> <c>area_mask</c>) were matched
/// against FS-UAE with exactly this scan-line fill plus the blitter edges.
/// </summary>
internal static class PillowPolygon
{
    private sealed class Edge
    {
        public int X0, Y0, XMin, YMin, XMax, YMax;
        public float Dx;
    }

    private static Edge MakeEdge(int x0, int y0, int x1, int y1) => new()
    {
        XMin = Math.Min(x0, x1), XMax = Math.Max(x0, x1),
        YMin = Math.Min(y0, y1), YMax = Math.Max(y0, y1),
        Dx = y0 == y1 ? 0f : (float)(x1 - x0) / (y1 - y0),
        X0 = x0, Y0 = y0,
    };

    /// <summary>Fill the polygon into <paramref name="mask"/> (<paramref name="width"/> x <paramref name="height"/>).</summary>
    public static void Fill(bool[] mask, int width, int height, IReadOnlyList<(int X, int Y)> xy)
    {
        var count = xy.Count;
        if (count < 2)
            throw new ArgumentException("coordinate list must contain at least 2 coordinates");
        var e = new List<Edge>();
        int i;
        for (i = 0; i < count - 1; i++)
        {
            var (x0, y0) = xy[i];
            var (x1, y1) = xy[i + 1];
            if (y0 == y1 && i != 0 && y0 == xy[i - 1].Y)
            {
                var last = e[^1];
                if (x1 > x0 && x0 > xy[i - 1].X)
                {
                    last.XMax = x1;
                    continue;
                }
                if (x1 < x0 && x0 < xy[i - 1].X)
                {
                    last.XMin = x1;
                    continue;
                }
            }
            e.Add(MakeEdge(x0, y0, x1, y1));
        }
        if (xy[i] != xy[0])
            e.Add(MakeEdge(xy[i].X, xy[i].Y, xy[0].X, xy[0].Y));
        Generic(mask, width, height, e);
    }

    private static void HLine(bool[] mask, int width, int height, int x0, int y0, int x1)
    {
        if (y0 < 0 || y0 >= height)
            return;
        if (x0 < 0)
            x0 = 0;
        else if (x0 >= width)
            return;
        if (x1 < 0)
            return;
        if (x1 >= width)
            x1 = width - 1;
        for (var x = x0; x <= x1; x++)
            mask[y0 * width + x] = true;
    }

    private static float RoundF(float v) => MathF.Round(v, MidpointRounding.AwayFromZero);

    private static int RoundUp(float f) => f >= 0f ? (int)MathF.Floor(f + 0.5f) : -(int)MathF.Floor(MathF.Abs(f) + 0.5f);

    private static int RoundDown(float f) => f >= 0f ? (int)MathF.Ceiling(f - 0.5f) : -(int)MathF.Ceiling(MathF.Abs(f) - 0.5f);

    private static float At(Edge edge, int y) => (float)(y - edge.Y0) * edge.Dx + edge.X0;

    private static void Generic(bool[] mask, int width, int height, List<Edge> e)
    {
        var n = e.Count;
        if (n <= 0)
            return;
        int ymin = height - 1, ymax = 0;
        var table = new List<Edge>();
        for (var i = 0; i < n; i++)
        {
            if (ymin > e[i].YMin)
                ymin = e[i].YMin;
            if (ymax < e[i].YMax)
                ymax = e[i].YMax;
            if (e[i].YMin == e[i].YMax)
            {
                HLine(mask, width, height, e[i].XMin, e[i].YMin, e[i].XMax);
                continue;
            }
            table.Add(e[i]);
        }
        if (ymin < 0)
            ymin = 0;
        if (ymax > height)
            ymax = height;
        var xx = new float[table.Count * 2];
        for (; ymin <= ymax; ymin++)
        {
            var j = 0;
            for (var i = 0; i < table.Count; i++)
            {
                var current = table[i];
                if (ymin < current.YMin || ymin > current.YMax)
                    continue;
                xx[j++] = At(current, ymin);
                if (ymin == current.YMax && ymin < ymax)
                {
                    xx[j] = xx[j - 1];
                    j++;
                }
                else if ((ymin == current.YMin || ymin == current.YMax) && current.Dx != 0)
                {
                    for (var k = 0; k < i; k++)
                    {
                        var other = table[k];
                        if ((ymin != other.YMin && ymin != other.YMax) || other.Dx == 0)
                            continue;
                        if (RoundF(xx[j - 1]) == RoundF(At(other, ymin)))
                        {
                            var offset = ymin == current.YMax ? -1 : 1;
                            var adjacent = At(current, ymin + offset);
                            if (ymin + offset >= other.YMin && ymin + offset <= other.YMax)
                            {
                                var adjacentOther = At(other, ymin + offset);
                                if (xx[j - 1] > adjacent + 1 && xx[j - 1] > adjacentOther + 1)
                                    xx[j - 1] = RoundF(MathF.Max(adjacent, adjacentOther)) + 1;
                                else if (xx[j - 1] < adjacent - 1 && xx[j - 1] < adjacentOther - 1)
                                    xx[j - 1] = RoundF(MathF.Min(adjacent, adjacentOther)) - 1;
                                break;
                            }
                        }
                    }
                }
            }
            Array.Sort(xx, 0, j);
            for (var i = 1; i < j; i += 2)
                HLine(mask, width, height, RoundUp(xx[i - 1]), ymin, RoundDown(xx[i]));
        }
    }
}
