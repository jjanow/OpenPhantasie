using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// s221: the C64 Phantasie I / II ending and party-wipe (defeat) pictures (<c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>
/// section 9, <c>…/phantasie2/GRAPHICS.md</c> section 8; the same recipe as <c>tools/scripts/extract_c64_end_screens.py</c>,
/// which writes the same files). I: <c>ZALL</c> (Zeus, his sprites and font, as <c>P.END</c> shows them). II:
/// <c>PH2.END-4</c> (the hall) and <c>TLUM1</c> (the crowd band, as <c>WIN</c> shows it). Both: <c>CEMETARY</c>'s temple
/// (<c>FONTO</c>), ghost, door and the Great Demon's face (<c>SKEL2</c> / <c>CSPRIT</c>). Full screens are 320 × 200 opaque,
/// sprites and the crowd strip transparent where the VIC shows the screen behind them. Positions are VIC sprite
/// coordinates (screen x + 24, y + 50), live in VICE.
/// </summary>
public static class C64EndScreens
{
    /// <summary>ZALL's sprite blocks $C0–$D0 as live (P.END): name, colour, multicolour (MC1, MC2) or null. $D1–$D2 are
    /// disk slack (directory entries), not pictures. Blocks not seen live are drawn white [?].</summary>
    public static readonly (int Block, string Name, int Colour, (int Mc1, int Mc2)? Multi)[] ZallSprites =
    [
        (0xC0, "hand-0", 1, null), (0xC1, "hand-1", 1, null), (0xC2, "hand-2", 1, null), (0xC3, "hand-3", 1, null),
        (0xC4, "hand-4", 1, null), (0xC5, "hand-5", 1, null), (0xC6, "face-0", 1, null), (0xC7, "face-1", 1, null),
        (0xC8, "face-2", 1, null), (0xC9, "face-3", 1, null), (0xCA, "face-4", 1, null), (0xCB, "face-5", 1, null),
        (0xCC, "face-6", 1, null), (0xCD, "face-7", 1, null), (0xCE, "orb", 1, null), (0xCF, "lightning-hand", 14, null),
        (0xD0, "cloud", 2, (7, 2)),
    ];

    /// <summary>
    /// II's <c>decor.demon-face</c>: the remaster draws that slot at the ST's (123, 19) over <c>ui.decor</c>; the C64's face
    /// sprite covers screen (124, 31)–(171, 72), so the slot takes <c>temple-demon.png</c>'s (123, 19)–(171, 72).
    /// </summary>
    public const int DoorwayX = 123, DoorwayY = 19, DoorwayW = 49, DoorwayH = 54;

    /// <summary>Where Zeus's lightning hand (block $CF, sprite 7 at VIC (202, 62)) lands on the screen: <c>ending.arm</c>.</summary>
    public const int ArmX = 202 - 24, ArmY = 62 - 50;

    /// <summary>A file's data without its load address; <paramref name="at"/> (null: any) and <paramref name="length"/> checked.</summary>
    private static byte[] Prg(Func<string, byte[]?> read, string name, int? at, int? length = null)
    {
        var prg = read(name) ?? throw new FileNotFoundException($"{name} not found in the C64 copy", name);
        if (prg.Length < 2 || at is int a && (prg[0] | prg[1] << 8) != a || length is int n && prg.Length - 2 != n)
            throw new InvalidDataException($"{name}: unexpected file (load address or length)");
        return prg[2..];
    }

    /// <summary>A 320 × 200 C64 display: VIC colours and a foreground mask (set character pixels) for sprite priority.</summary>
    private sealed class Screen
    {
        private readonly int[] _px = new int[320 * 200];
        private readonly bool[] _fg = new bool[320 * 200];

        /// <summary>Hires text: <paramref name="codes"/> / <paramref name="colours"/> per cell (code −1 = leave the cell).</summary>
        public void Chars(ReadOnlySpan<byte> charset, int[,] codes, Func<int, int, int> colour)
        {
            for (var row = 0; row < 25; row++)
            for (var col = 0; col < 40; col++)
            {
                var code = codes[row, col];
                if (code < 0)
                    continue;
                for (var r = 0; r < 8; r++)
                {
                    var b = code * 8 + r < charset.Length ? charset[code * 8 + r] : 0;
                    for (var p = 0; p < 8; p++)
                    {
                        var on = (b >> (7 - p) & 1) != 0;
                        var i = (row * 8 + r) * 320 + col * 8 + p;
                        if (on)
                            _px[i] = colour(row, col);
                        _fg[i] = on;
                    }
                }
            }
        }

        /// <summary>A sprite at VIC (<paramref name="vx"/>, <paramref name="vy"/>); <paramref name="behind"/>: under the characters' set pixels.</summary>
        public void Sprite(ReadOnlySpan<byte> block, int vx, int vy, int colour, bool expand = false, bool behind = false)
        {
            foreach (var (y, x, c) in SpritePixels(block, colour, expand, expand, null))
            {
                int sx = vx - 24 + x, sy = vy - 50 + y;
                if (sx is >= 0 and < 320 && sy is >= 0 and < 200 && !(behind && _fg[sy * 320 + sx]))
                    _px[sy * 320 + sx] = c;
            }
        }

        public RgbaImage Image()
        {
            var img = new RgbaImage(320, 200);
            for (var i = 0; i < _px.Length; i++)
                C64Gfx.Plot(img, i % 320, i / 320, _px[i]);
            return img;
        }
    }

    private static List<(int Y, int X, int C)> SpritePixels(ReadOnlySpan<byte> block, int colour, bool xexp, bool yexp,
        (int Mc1, int Mc2)? multi)
    {
        var list = new List<(int, int, int)>();
        int xs = xexp ? 2 : 1, ys = yexp ? 2 : 1;
        for (var r = 0; r < 21; r++)
        {
            var bits = block[3 * r] << 16 | block[3 * r + 1] << 8 | block[3 * r + 2];
            for (var p = 0; p < 24; p++)
            {
                int c;
                if (multi is { } m)
                {
                    var k = bits >> (22 - (p & ~1)) & 3;
                    if (k == 0)
                        continue;
                    c = k switch { 1 => m.Mc1, 2 => colour, _ => m.Mc2 };
                }
                else if ((bits >> (23 - p) & 1) != 0)
                    c = colour;
                else
                    continue;
                for (var a = 0; a < xs; a++)
                for (var b = 0; b < ys; b++)
                    list.Add((r * ys + b, p * xs + a, c));
            }
        }
        return list;
    }

    private static RgbaImage SpriteImage(ReadOnlySpan<byte> block, int colour, bool expand = false, (int, int)? multi = null)
    {
        var img = new RgbaImage(24 * (expand ? 2 : 1), 21 * (expand ? 2 : 1));
        foreach (var (y, x, c) in SpritePixels(block, colour, expand, expand, multi))
            C64Gfx.Plot(img, x, y, c);
        return img;
    }

    /// <summary>The first <paramref name="count"/> characters as a 16-wide sheet, white on black.</summary>
    private static RgbaImage FontSheet(ReadOnlySpan<byte> charset, int count)
    {
        var img = new RgbaImage(128, 8 * ((count + 15) / 16));
        for (var i = 0; i < img.Pixels.Length; i += 4)
            img.Pixels[i + 3] = 255; // black
        for (var code = 0; code < count; code++)
            C64Gfx.DrawChar(img, code % 16 * 8, code / 16 * 8, charset.Slice(code * 8, 8), 1, null);
        return img;
    }

    /// <summary><c>SYS Q,x0,y0,x1,y1,colour,first</c> (UTIL-3.O): codes <paramref name="first"/>… numbered down each column.</summary>
    private static int[,] Rect(int first, int col0, int row0, int cols, int rows)
    {
        var codes = new int[25, 40];
        for (var r = 0; r < 25; r++)
        for (var c = 0; c < 40; c++)
            codes[r, c] = -1;
        for (var c = 0; c < cols; c++)
        for (var r = 0; r < rows; r++)
            codes[row0 + r, col0 + c] = first + c * rows + r;
        return codes;
    }

    private static byte[] Block(byte[] blocks, int n) => blocks.AsSpan(n * 64, 63).ToArray();

    /// <summary>I's ending (<c>P.END</c> + <c>ZALL</c>, side 1): Zeus and his states, the sprite blocks, the font.</summary>
    public static IEnumerable<(string Rel, RgbaImage Image)> P1Ending(Func<string, byte[]?> read)
    {
        var zall = Prg(read, "ZALL", 0x4000, 3716);
        var charset = new byte[2048];
        zall.AsSpan(0, 0x7FF).CopyTo(charset); // P.END's copies leave the last byte (char $FF row 7) at 0 (live)
        var sprites = new byte[19 * 64];
        zall.AsSpan(0x800, 1216).CopyTo(sprites); // $4800 → $F000 = blocks $C0…$D2
        byte[] B(int n) => Block(sprites, n - 0xC0);

        Screen Zeus(params (int Row, int Col)[] blank)
        {
            var codes = Rect(0x60, 13, 2, 12, 13);
            foreach (var (row, col) in blank) // P.END prints spaces where a sprite takes over (live)
                codes[row, col] = 0x20;
            var s = new Screen();
            s.Chars(charset, codes, (_, _) => 1);
            return s;
        }

        (int, int)[] face = [(4, 18), (4, 19), (5, 18), (5, 19)];
        (int, int)[] hand = [(3, 22), (3, 23), (3, 24), (4, 22), (4, 23), (4, 24)];
        yield return ("ending/zeus.png", Zeus().Image());
        var speech = Zeus(face); // the speech: face sprite 6 (frames $C6, $C7, $CC, $C9) at VIC (161, 77)
        speech.Sprite(B(0xC6), 0xA1, 0x4D, 1);
        yield return ("ending/zeus-speech.png", speech.Image());
        var grant = Zeus([.. face, .. hand]); // the grant: also the hand, sprite 7 (frames $C0–$C5) at VIC (200, 74)
        grant.Sprite(B(0xC6), 0xA1, 0x4D, 1);
        grant.Sprite(B(0xC5), 0xC8, 0x4A, 1);
        yield return ("ending/zeus-grant.png", grant.Image());
        var strike = Zeus(); // a strike: sprite 7 = $CF, light blue, at VIC (202, 62), flashing
        strike.Sprite(B(0xCF), ArmX + 24, ArmY + 50, 14);
        yield return ("ending/zeus-strike.png", strike.Image());
        foreach (var (block, name, colour, multi) in ZallSprites)
            yield return ($"ending/sprites/{block:x2}-{name}.png", SpriteImage(B(block), colour, multi: multi));
        yield return ("ending/font.png", FontSheet(charset, 96));
    }

    /// <summary>
    /// The party wipe (<c>CEMETARY</c> / <c>CEMETARY2</c>; <c>SKEL2</c>, <c>CSPRIT</c>, <c>FONTO</c>, each game's own side 2):
    /// the temple (open doorway, closed door, the face), the ghost, door and face sprites, the font;
    /// <paramref name="doorway"/>: also the face's doorway for an ST-placed <c>decor.demon-face</c> (II).
    /// </summary>
    public static IEnumerable<(string Rel, RgbaImage Image)> Defeat(Func<string, byte[]?> read, bool doorway)
    {
        var skel = Prg(read, "SKEL2", 0x2000, 512);
        var csprit = Prg(read, "CSPRIT", null, 1025); // loaded to $2000 by CEMETARY's SYS whatever the file says ($2300)
        var fonto = Prg(read, "FONTO", null, 1968);
        var charset = new byte[2048];
        fonto.CopyTo(charset, 0);
        byte[] ghost = Block(skel, 0), face = Block(skel, 6), door = Block(skel, 7); // blocks $84, $8A, $8B
        if (!face.AsSpan().SequenceEqual(Block(csprit, 15)))
            throw new InvalidDataException("SKEL2's face is not CSPRIT block 15");
        var codes = Rect(0x40, 10, 0, 18, 10);
        Screen Temple()
        {
            var s = new Screen();
            s.Chars(charset, codes, (_, _) => 11);
            return s;
        }

        yield return ("defeat/temple.png", Temple().Image());
        var shut = Temple();
        shut.Sprite(door, 0x94, 0x4D, 15, expand: true, behind: true);
        yield return ("defeat/temple-door.png", shut.Image());
        var demon = Temple();
        demon.Sprite(face, 0x94, 0x51, 4, expand: true);
        var demonImage = demon.Image();
        yield return ("defeat/temple-demon.png", demonImage);
        if (doorway)
            yield return ("defeat/temple-demon-doorway.png", C64Gfx.Crop(demonImage, DoorwayX, DoorwayY, DoorwayW, DoorwayH));
        yield return ("defeat/ghost.png", SpriteImage(ghost, 1));
        yield return ("defeat/door.png", SpriteImage(door, 15, expand: true));
        yield return ("defeat/demon-face.png", SpriteImage(face, 4, expand: true));
        yield return ("defeat/font.png", FontSheet(charset, 64));
    }

    /// <summary>TLUM1's band as WIN shows it: sprites 0–6 (enable $7F; WIN pokes sprite 6's x to 47 + 256), blocks + 8 per frame.</summary>
    private static RgbaImage CrowdStrip(ReadOnlySpan<byte> band, byte[] blocks, int frame)
    {
        var img = new RgbaImage(320, 42);
        for (var i = 0; i < 7; i++)
        {
            var x0 = i == 6 ? 47 + 256 : band[1 + 2 * i]; // WIN: POKE A1 + 13, 47 and $D010 := 64 (the file holds 0)
            var block = Block(blocks, band[26 + i] + 8 * frame);
            foreach (var (y, x, c) in SpritePixels(block, band[17 + i] & 15, true, true, null))
                if (x0 - 24 + x is >= 0 and < 320)
                    C64Gfx.Plot(img, x0 - 24 + x, y, c);
        }
        return img;
    }

    /// <summary>II's ending (<c>WIN</c>: <c>PH2.END-4</c>, <c>TLUM1</c>, side 2): the hall, the crowd's two frames, both.</summary>
    public static IEnumerable<(string Rel, RgbaImage Image)> P2Ending(Func<string, byte[]?> read)
    {
        var pic = Prg(read, "PH2.END-4", 0x2000, 4095);
        var charset = new byte[2048];
        pic.AsSpan(2048, 2047).CopyTo(charset); // WIN copies 2,047 bytes to $E800
        var codes = new int[25, 40];
        for (var i = 0; i < 1000; i++)
            codes[i / 40, i % 40] = pic[1024 + i];
        var s = new Screen();
        s.Chars(charset, codes, (row, col) => pic[row * 40 + col] & 15);
        var hall = s.Image();
        yield return ("ending/hall.png", hall);
        var tlum = Prg(read, "TLUM1", 0x1FDA);
        var band = tlum.AsSpan(0, SpriteBand.Size).ToArray();
        var blocks = new byte[16 * 64];
        tlum.AsSpan(SpriteBand.Size).CopyTo(blocks);
        var frames = new[] { CrowdStrip(band, blocks, 0), CrowdStrip(band, blocks, 1) };
        yield return ("ending/crowd-0.png", frames[0]);
        yield return ("ending/crowd-1.png", frames[1]);
        var top = band[0] - 50; // band y $A0: every sprite at VIC y 160 (live)
        var both = new RgbaImage(320, 200);
        Array.Copy(hall.Pixels, both.Pixels, hall.Pixels.Length);
        for (var y = 0; y < frames[0].Height; y++)
        for (var x = 0; x < 320; x++)
        {
            var i = (y * 320 + x) * 4;
            if (frames[0].Pixels[i + 3] != 0)
                Array.Copy(frames[0].Pixels, i, both.Pixels, ((top + y) * 320 + x) * 4, 4);
        }
        yield return ("ending/hall-crowd.png", both);
    }
}
