namespace OpenPhantasie.Data;

/// <summary>
/// s224: Applesoft's hi-res page 1 and its drawing statements as the Apple II+ ROM runs them, for replaying a game's BASIC
/// drawings (Phantasie I's <c>CEMETARY</c> / <c>FLASH?</c>; <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> 7).
/// <see cref="Mem"/> is the 8 KiB page as in memory (offset 0 = <c>$2000</c>, the screen's interleave and its holes).
/// The routines follow the ROM: <c>HCLR</c> ($F3F2), <c>HPOSN</c> ($F411; the colour byte swaps on odd byte columns),
/// <c>HPLOT</c> ($F457), <c>HPLOT TO</c> ($F53A), <c>DRAW</c> ($F601) / <c>XDRAW</c> ($F65D) with any <c>ROT</c> (the
/// cosine table at $F5BA; each step a major and a minor carry, each may plot then move) and the cursor moves ($F465 left /
/// right, which wrap within the row; $F4D3 up / down on the base address, as the ROM computes it). Checked against the
/// ROM run in a 6502 emulator (byte-equal pages, s224). No rendering types.
/// </summary>
public sealed class Apple2Hires
{
    private static readonly byte[] Cos =
        [0xFF, 0xFE, 0xFA, 0xF4, 0xEC, 0xE1, 0xD4, 0xC5, 0xB4, 0xA1, 0x8D, 0x78, 0x61, 0x49, 0x31, 0x18, 0xFF];

    private static readonly byte[] Masks = [0x81, 0x82, 0x84, 0x88, 0x90, 0xA0, 0xC0];

    /// <summary>HCOLOR= 0..7 → the colour byte ($F6F6).</summary>
    public static readonly byte[] HcolorBytes = [0x00, 0x2A, 0x55, 0x7F, 0x80, 0xAA, 0xD5, 0xFF];

    public const int Width = 280;
    public const int Height = 192;

    public byte[] Mem { get; } = new byte[0x2000];

    private int _basl, _bash = 0x20, _horiz, _hmask = 0x81, _color1, _hcolor;
    private int _lastX, _lastY;

    /// <summary>Bumped on every write, so a view can tell when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>HCLR: every byte of the page 0 (black).</summary>
    public void Clear()
    {
        Array.Clear(Mem);
        Version++;
    }

    /// <summary>The page's memory as a copy (for a test's hash or a snapshot).</summary>
    public byte[] Snapshot() => (byte[])Mem.Clone();

    public void Load(ReadOnlySpan<byte> page)
    {
        page[..Math.Min(page.Length, Mem.Length)].CopyTo(Mem);
        Version++;
    }

    public void SetHcolor(int n) => _hcolor = HcolorBytes[n & 7];

    /// <summary>HPOSN: the cursor to (x, y), the colour byte set for its byte column.</summary>
    public void Position(int x, int y)
    {
        _lastX = x;
        _lastY = y;
        var b = 0x2000 + ((y & 7) << 10) + (((y >> 3) & 7) << 7) + (y >> 6) * 40;
        _basl = b & 0xFF;
        _bash = b >> 8;
        _horiz = x / 7;
        _hmask = Masks[x % 7];
        _color1 = _hcolor;
        if ((_horiz & 1) != 0)
            Flip();
    }

    /// <summary>HPLOT x, y.</summary>
    public void Plot(int x, int y)
    {
        Position(x, y);
        PlotHere();
    }

    /// <summary>HPLOT x, y TO x1, y1 TO …: the first point, then a line to each next one.</summary>
    public void Lines(IReadOnlyList<int> points)
    {
        if (points.Count < 2)
            return;
        Plot(points[0], points[1]);
        for (var i = 2; i + 1 < points.Count; i += 2)
            LineTo(points[i], points[i + 1]);
    }

    /// <summary>DRAW (or XDRAW) shape <paramref name="n"/> (1-based) of <paramref name="table"/> at (x, y).</summary>
    public void Draw(ReadOnlySpan<byte> table, int n, int x, int y, int rot, int scale, bool xor)
    {
        if (table.Length < 2 || n < 1 || n > table[0])
            return;
        Position(x, y);
        var p = table[2 + 2 * (n - 1)] | table[3 + 2 * (n - 1)] << 8;
        var quad = (rot >> 4) & 0xFF;
        int dx = Cos[rot & 0x0F], dy = (Cos[16 - (rot & 0x0F)] + 1) & 0xFF;
        while (p < table.Length && table[p] != 0)
        {
            for (int d1 = table[p]; d1 != 0; d1 >>= 3)
            {
                int r4 = 0x80, r5 = 0x80;
                for (var i = 0; i < (scale == 0 ? 256 : scale); i++)
                {
                    var t = r4 + dx + 1;
                    r4 = t & 0xFF;
                    if (t > 0xFF)
                        Vector(d1, quad, 0, xor);
                    t = r5 + dy;
                    r5 = t & 0xFF;
                    if (t > 0xFF)
                        Vector(d1, quad, 1, xor);
                }
            }
            p++;
        }
        Version++;
    }

    private void Vector(int d1, int quad, int extra, bool xor)
    {
        if ((d1 & 4) != 0)
        {
            if (xor)
                Put(Get() ^ (_hmask & 0x7F));
            else
                PlotHere();
        }
        Move((d1 + quad + extra) & 3);
    }

    private void Move(int dir)
    {
        switch (dir)
        {
            case 0: Up(); break;
            case 1: Right(); break;
            case 2: Down(); break;
            default: Left(); break;
        }
    }

    private int Address => ((_bash << 8) | _basl) + _horiz;

    private int Get()
    {
        var a = Address;
        return a is >= 0x2000 and < 0x4000 ? Mem[a - 0x2000] : 0;
    }

    /// <summary>A write outside $2000–$3FFF is dropped (on the Apple it lands in other memory).</summary>
    private void Put(int v)
    {
        var a = Address;
        if (a is >= 0x2000 and < 0x4000)
            Mem[a - 0x2000] = (byte)v;
    }

    private void PlotHere()
    {
        var old = Get();
        Put(old ^ ((old ^ _color1) & _hmask));
        Version++;
    }

    /// <summary>$F47C: a two-colour byte swaps its pattern on a byte-column change.</summary>
    private void Flip()
    {
        var a = (_color1 << 1) & 0xFF;
        if (((a - 0xC0) & 0x80) != 0)
            _color1 ^= 0x7F;
    }

    private void Left()
    {
        var c = _hmask & 1;
        var m = _hmask >> 1;
        if (c == 0)
        {
            _hmask = m ^ 0xC0;
            return;
        }
        _horiz = _horiz == 0 ? 0x27 : _horiz - 1;
        _hmask = 0xC0;
        Flip();
    }

    private void Right()
    {
        var a = ((_hmask << 1) & 0xFF) ^ 0x80;
        if ((a & 0x80) != 0)
        {
            _hmask = a;
            return;
        }
        _horiz = _horiz + 1 >= 0x28 ? 0 : _horiz + 1;
        _hmask = 0x81;
        Flip();
    }

    private static int Adc(int a, int v, ref int c)
    {
        var r = a + v + c;
        c = r > 0xFF ? 1 : 0;
        return r & 0xFF;
    }

    /// <summary>$F4D5: one row up, on the base address as the ROM steps it.</summary>
    private void Up()
    {
        var a = _bash;
        var c = 0;
        if ((a & 0x1C) != 0)
        {
            _bash = Adc(a, 0xFC, ref c);
            return;
        }
        c = (_basl >> 7) & 1;
        _basl = (_basl << 1) & 0xFF;
        if (c != 0)
            a = Adc(a, 0x1F, ref c);
        else if ((a & 0x03) != 0)
        {
            a = Adc(a, 0x1F, ref c);
            c = 1;
        }
        else
        {
            a = Adc(a, 0x23, ref c);
            var l = Adc(_basl, 0xB0, ref c);
            if (c == 0)
                l = Adc(l, 0xF0, ref c);
            _basl = l;
            if (c == 0)
                a = Adc(a, 0x1F, ref c);
        }
        var nc = _basl & 1;
        _basl = ((c << 7) | (_basl >> 1)) & 0xFF;
        _bash = Adc(a, 0xFC, ref nc);
    }

    /// <summary>$F505: one row down.</summary>
    private void Down()
    {
        var c = 0;
        var a = Adc(_bash, 0x04, ref c);
        if ((a & 0x1C) != 0)
        {
            _bash = a;
            return;
        }
        c = (_basl >> 7) & 1;
        _basl = (_basl << 1) & 0xFF;
        if (c == 0)
            a = Adc(a, 0xE0, ref c);
        else
        {
            a = Adc(a, 0xE0, ref c);
            c = 0;
            if ((a & 0x04) != 0)
            {
                var l = Adc(_basl, 0x50, ref c);
                l ^= 0xF0;
                if (l != 0)
                    l ^= 0xF0;
                _basl = l;
                a = 0x20;
                if (c != 0)
                    a = Adc(a, 0xE0, ref c);
            }
        }
        _basl = ((c << 7) | (_basl >> 1)) & 0xFF;
        _bash = a;
    }

    /// <summary>HPLOT TO x, y from the last point ($F53A: the ROM's stepping, the end point included).</summary>
    public void LineTo(int x, int y)
    {
        int x0 = _lastX, y0 = _lastY;
        var dx = x - x0;
        var d3 = dx < 0 ? 0x80 : 0;
        var adx = Math.Abs(dx);
        int d0 = adx & 0xFF, d1 = adx >> 8;
        int d4 = d0, d5 = d1;
        _lastX = x;
        var c = y - y0 - 1 >= 0 ? 1 : 0;
        var a = (y - y0 - 1) & 0xFF;
        if (c != 0)
            a = Adc(a ^ 0xFF, 0xFE, ref c);
        var d2 = a;
        _lastY = y;
        d3 = ((c << 7) | (d3 >> 1)) & 0xFF;
        var r = d2 - d0;
        var xr = r & 0xFF;
        c = r < 0 ? 0 : 1;
        r = 0xFF - d1 - (1 - c);
        var countHi = r & 0xFF;
        c = 1;
        while (true)
        {
            // $F581: the error += D2.
            var t = d4 + d2 + c;
            d4 = t & 0xFF;
            c = t > 0xFF ? 1 : 0;
            t = d5 - (1 - c);
            d5 = t & 0xFF;
            c = t < 0 ? 0 : 1;
            while (true)
            {
                PlotHere();
                xr = (xr + 1) & 0xFF;
                if (xr == 0)
                {
                    countHi = (countHi + 1) & 0xFF;
                    if (countHi == 0)
                        return;
                }
                if (c != 0)
                {
                    if (((d3 << 1) & 0x80) != 0)
                        Left();
                    else
                        Right();
                    c = 1;
                    break;
                }
                if ((d3 & 0x80) != 0)
                    Down();
                else
                    Up();
                t = d4 + d0;
                d4 = t & 0xFF;
                c = t > 0xFF ? 1 : 0;
                t = d5 + d1 + c;
                d5 = t & 0xFF;
                c = t > 0xFF ? 1 : 0;
            }
        }
    }

    /// <summary>The page's byte for screen row <paramref name="y"/>, byte column <paramref name="col"/>.</summary>
    public static int Offset(int y, int col) => ((y & 7) << 10) + (((y >> 3) & 7) << 7) + (y >> 6) * 40 + col;

    private static readonly (byte R, byte G, byte B)[] Rgb =
    [
        (0, 0, 0), (255, 255, 255), (255, 68, 253), (20, 245, 60), (20, 207, 253), (255, 106, 60),
    ];

    private const int Black = 0, White = 1, Violet = 2, Green = 3, Blue = 4, Orange = 5;

    private static int Artifact(int x, bool high) => high ? ((x & 1) != 0 ? Orange : Blue) : ((x & 1) != 0 ? Green : Violet);

    /// <summary>
    /// Rows <paramref name="y0"/>..<paramref name="y1"/>−1 of <paramref name="page"/> as RGBA (280 wide), in the NTSC artifact
    /// model of the importer's Apple packs (<c>HiresPage.ToImage</c>: a lit pixel next to a lit one is white, a lone one
    /// takes its column's colour by the byte's high bit, a gap between two lone pixels of a colour fills) `[~]`.
    /// </summary>
    public static byte[] ToRgba(ReadOnlySpan<byte> page, int y0, int y1)
    {
        var rgba = new byte[Width * (y1 - y0) * 4];
        var bits = new int[Width];
        var high = new bool[Width];
        for (var y = y0; y < y1; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var v = page[Offset(y, x / 7)];
                bits[x] = (v >> (x % 7)) & 1;
                high[x] = (v & 0x80) != 0;
            }
            for (var x = 0; x < Width; x++)
            {
                var left = x > 0 ? bits[x - 1] : 0;
                var right = x + 1 < Width ? bits[x + 1] : 0;
                int colour;
                if (bits[x] != 0)
                    colour = left != 0 || right != 0 ? White : Artifact(x, high[x]);
                else if (left != 0 && right != 0)
                {
                    var loneLeft = (x > 1 ? bits[x - 2] : 0) == 0;
                    var loneRight = (x + 2 < Width ? bits[x + 2] : 0) == 0;
                    colour = loneLeft && loneRight ? Artifact(x - 1, high[x - 1]) : Black;
                }
                else
                    colour = Black;
                var (r, g, b) = Rgb[colour];
                var o = ((y - y0) * Width + x) * 4;
                rgba[o] = r;
                rgba[o + 1] = g;
                rgba[o + 2] = b;
                rgba[o + 3] = 255;
            }
        }
        return rgba;
    }
}
