using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P3Dos;

/// <summary>
/// The DOS CGA art pack of III (s160; pack <c>GameData/phantasie3/graphics/dos-cga.json</c>): a port of
/// <c>extract_dos_p3_graphics.py</c>, formats in <c>reverse-engineering/dos/phantasie3/FORMAT.md</c> section 10. Paths
/// are relative to the pack root. Every picture is CGA 2 bits a pixel, rows one after another, in palette 0 high.
/// </summary>
public static class P3CgaArt
{
    /// <summary>The pack root (<c>dos-cga.json</c> "root"), kept under the import's <c>files/</c>.</summary>
    public const string Root = "reverse-engineering/dos/phantasie3/extracted/graphics";

    /// <summary>The picture files and their sha1 (<c>unpacked/README.md</c>); read at fixed offsets.</summary>
    public static readonly IReadOnlyList<(string Name, string? Alias, string Sha1)> Sources =
    [
        ("TOWN.CMP", "DTOWN.CMP", "642b4e3eee2005721a63306fc286adac61b428fe"),
        ("PICS.CMP", null, "a4c9c882da94e4eb60652fe6b65667595364dde3"),
        ("WORLD.CGA", null, "3ac9f4ac2def8f523a9856266a553767c52be739"),
        ("SHAPES.CGA", null, "e7a81eb928003f5ab3be00499f6413a998dbaa3b"),
        ("MONSTERS.CGA", null, "088129fbad2e3fcd26f680bf9210033b442703c9"),
    ];

    /// <summary>CGA palette 0, high intensity (DOSBox colours).</summary>
    private static readonly (byte R, byte G, byte B)[] Palette = [(0, 0, 0), (0x55, 0xFF, 0x55), (0xFF, 0x55, 0x55), (0xFF, 0xFF, 0x55)];

    private static readonly string[] CoreRaces = ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE",
        "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN", "LIZARDMAN", "MINOTAUR", "UNDEAD"];

    /// <summary>III race byte → first picture of its pair (DS:0x9ea); entry 18 = the grave.</summary>
    private static readonly int[] RacePicture = [0, 2, 4, 6, 6, 0, 14, 16, 16, 10, 12, 14, 16, 8, 8, 20, 22, 18, 24];

    private static readonly (string Name, string Note)[] Pics =
    [
        ("title", "the title (ovl1 0x7f7)"), ("astral-plane", "the party defeat (root 0x1edd)"),
        ("reward-good", "Zeus (ovl4 0xae)"), ("reward-evil", "Pluto (ovl4 0xc9)"),
    ];

    private const int ShapeSparkle = 0x0000, ShapeGlyphs8 = 0x01B0, ShapeParty = 0x02A0, ShapeDungeon = 0x2820,
        ShapeScandor = 0x2890, ShapeBody = 0x3C40;

    /// <summary>Dungeon glyph number → slot (ovl4 0x5a8, jump table 0x5f7).</summary>
    private static readonly (int Glyph, int Slot)[] DungeonSlot = [(46, 1), (47, 2), (48, 3), (49, 6), (50, 5), (51, 4), (52, 0)];

    /// <summary>Body figure pieces in draw order (root 0xdcf): the stored piece of each; file = 9 · set + i.</summary>
    private static readonly int[] BodyPieces = [4, 1, 5, 9, 2, 6, 10, 3, 11];

    private const int ScreenBytes = 16000;

    /// <summary>
    /// Why the pictures can't be cut from <paramref name="files"/> (a file missing or another build's), or null when
    /// they can.
    /// </summary>
    public static string? Missing(FileSet files)
    {
        foreach (var (name, alias, sha1) in Sources)
        {
            var f = files.Find(name) ?? (alias is null ? null : files.Find(alias));
            if (f is null)
                return $"{name} not found";
            if (f.Sha1 != sha1)
                return $"{name} is not the SSI 1988 file (sha1 {f.Sha1})";
        }
        return null;
    }

    /// <summary>
    /// 2-bpp rows (4 pixels a byte, high bits first) to an image; with <paramref name="clearZero"/> colour 0 stays
    /// transparent (the flash, root <c>0x1537</c>: an OR blit).
    /// </summary>
    public static RgbaImage Cga(ReadOnlySpan<byte> data, int width, int height, bool clearZero = false)
    {
        var perRow = width / 4;
        if (data.Length < perRow * height)
            throw new InvalidDataException($"{data.Length} bytes for a {width}x{height} picture");
        var im = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (data[y * perRow + x / 4] >> (6 - 2 * (x % 4))) & 3;
                var c = Palette[i];
                im.Set(x, y, c.R, c.G, c.B, clearZero && i == 0 ? (byte)0 : (byte)255);
            }
        return im;
    }

    /// <summary>
    /// One packed screen at <paramref name="at"/> (root 0x284:0): byte 0 picks row (1) or column (2) order; runs c 1..127
    /// literal, c ≥ 128 the next byte 256 − c times, c = 0 a u16 count then the byte; 16,000 bytes out.
    /// </summary>
    public static (byte[] Screen, int End) Unpack(ReadOnlySpan<byte> src, int at)
    {
        var method = src[at];
        if (method is not (1 or 2))
            throw new InvalidDataException($"packed screen at {at}: unpacker {method}");
        var i = at + 1;
        var output = new byte[ScreenBytes];
        int di = 0, left = ScreenBytes, rows = 200;

        void Put(byte v)
        {
            output[di] = v;
            left--;
            if (method == 2)
            {
                di += 80;
                if (--rows == 0)
                {
                    rows = 200;
                    di -= ScreenBytes - 1;
                }
            }
            else
                di++;
        }

        while (left > 0)
        {
            var c = src[i];
            int n;
            bool run;
            if (c == 0)
                (n, i, run) = (src[i + 1] | (src[i + 2] << 8), i + 3, true);
            else if (c >= 0x80)
                (n, i, run) = (256 - c, i + 1, true);
            else
                (n, i, run) = (c, i + 1, false);
            if (run)
            {
                var v = src[i++];
                for (var k = 0; k < n && left > 0; k++)
                    Put(v);
            }
            else
                for (var k = 0; k < n && left > 0; k++)
                    Put(src[i++]);
        }
        return (output, i);
    }

    /// <summary>(offset, giant) of a monster's picture in MONSTERS.CGA (root 0x10b8b → 0x7e89).</summary>
    public static (int Offset, bool Giant) MonsterPicture(int byte27)
    {
        var p = byte27 > 27 ? byte27 - 1 : byte27;
        if (p is >= 41 and <= 46)
            return (0x7B00 + 2880 * (p - 41), true);
        if (p is < 0 or > 46)
            p = 15;
        return (768 * p, false);
    }

    /// <summary>
    /// D3-067 (user s126 / s161): the monsters whose picture DOS gets wrong borrow an analogous creature's, as the Amiga
    /// pack does: Wolf (byte 27 = 65) the Wild Dog's, Mud Elemental (48) the party elemental's, Troll Baby (27) the party
    /// Troll's (DOS: the Skeleton's / the Forest Elf's). Byte 27 → (from SHAPES.CGA's party pictures, picture index).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (bool Party, int Picture)> BorrowedPicture =
        new Dictionary<int, (bool, int)> { [65] = (false, 0), [48] = (true, RacePicture[16]), [27] = (true, RacePicture[15]) };

    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// Every PNG of the pack, by path under <see cref="Root"/>. <paramref name="coreRace"/> is <c>rule-tables.json</c>'s
    /// <c>core_race</c> (III race byte → remaster race name), which names the party pictures; without it only the
    /// elemental's and the grave are cut.
    /// </summary>
    public static Dictionary<string, RgbaImage> Pictures(Func<string, byte[]> read, JsonObject? coreRace)
    {
        var images = new Dictionary<string, RgbaImage>(StringComparer.Ordinal);

        images["screens/town.png"] = Cga(Unpack(read("TOWN.CMP"), 0).Screen, 320, 200);
        var pics = read("PICS.CMP");
        var at = 0;
        foreach (var (name, _) in Pics)
        {
            var (screen, end) = Unpack(pics, at);
            images[$"screens/{name}.png"] = Cga(screen, 320, 200);
            at = end;
        }
        if (at != pics.Length)
            throw new InvalidDataException($"PICS.CMP: {pics.Length - at} bytes after the fourth screen");

        var world = read("WORLD.CGA");
        for (var t = 0; t < 250; t++)
            images[$"overworld/tiles/{t:000}.png"] = Cga(world.AsSpan(64 * t, 64), 16, 16);

        var shapes = read("SHAPES.CGA");
        if (shapes.Length != 16192)
            throw new InvalidDataException($"SHAPES.CGA: {shapes.Length} bytes");
        for (var race = 0; race < 18; race++)
        {
            var p = RacePicture[race];
            string stem;
            if (race == 16)
                stem = "elemental";
            else if (coreRace?[race.ToString()]?.GetValue<string>() is { } core)
                stem = $"{Array.IndexOf(CoreRaces, core):00}-{Slug(core)}";
            else
                continue;
            foreach (var (frame, kind) in new[] { (0, "idle"), (1, "attacking") })
                images[$"portraits/{kind}/{stem}.png"] = Cga(shapes.AsSpan(ShapeParty + 384 * (p + frame), 384), 32, 48);
        }
        images["portraits/grave.png"] = Cga(shapes.AsSpan(ShapeParty + 384 * 24, 384), 32, 48);
        foreach (var (glyph, slot) in DungeonSlot)
            images[$"dungeon/glyph-{glyph}.png"] = Cga(shapes.AsSpan(ShapeDungeon + 16 * slot, 10), 8, 5);
        images["scroll/scandor.png"] = Cga(shapes.AsSpan(ShapeScandor, 5040), 120, 168);
        for (var set = 0; set < 4; set++)
            for (var i = 0; i < BodyPieces.Length; i++)
                images[$"body/{9 * set + i}.png"] = Cga(shapes.AsSpan(ShapeBody + 192 * set + 16 * BodyPieces[i], 16), 8, 8);
        for (var k = 0; k < 3; k++)
            images[$"shapes/frame24-{k}.png"] = Cga(shapes.AsSpan(ShapeSparkle + 144 * k, 144), 24, 24, clearZero: true);
        for (var k = 0; k < 15; k++)
            images[$"shapes/glyph8-{k:00}.png"] = Cga(shapes.AsSpan(ShapeGlyphs8 + 16 * k, 16), 8, 8);

        var creature = read("CREATURE.DAT");
        var monsters = read("MONSTERS.CGA");
        if (monsters.Length != 48768)
            throw new InvalidDataException($"MONSTERS.CGA: {monsters.Length} bytes");
        for (var mid = 0; mid < 80; mid++)
        {
            var raw = creature.AsSpan(38 * mid, 14);
            var z = raw.IndexOf((byte)0);
            var name = Encoding.ASCII.GetString(z < 0 ? raw : raw[..z]).Trim();
            var byte27 = creature[38 * mid + 27];
            if (BorrowedPicture.TryGetValue(byte27, out var borrowed) && borrowed.Party)
            {
                foreach (var (frame, kind) in new[] { (0, "idle"), (1, "attacking") })
                    images[$"monsters/{kind}/{mid:00}-{Slug(name)}.png"] =
                        Cga(shapes.AsSpan(ShapeParty + 384 * (borrowed.Picture + frame), 384), 32, 48);
                continue;
            }
            var (offset, giant) = MonsterPicture(BorrowedPicture.TryGetValue(byte27, out borrowed) ? borrowed.Picture : byte27);
            var (w, h) = giant ? (80, 72) : (32, 48);
            var size = w * h / 4;
            foreach (var (frame, kind) in new[] { (0, "idle"), (1, "attacking") })
                images[$"monsters/{kind}/{mid:00}-{Slug(name)}.png"] = Cga(monsters.AsSpan(offset + frame * size, size), w, h);
        }
        return images;
    }

    /// <summary>Reads a picture file by its name, or by its alias (a data disk's <c>DTOWN.CMP</c>).</summary>
    public static Func<string, byte[]> Reader(FileSet files) => name =>
    {
        var alias = Sources.FirstOrDefault(s => s.Name == name).Alias;
        return (files.Find(name) ?? (alias is null ? null : files.Find(alias)))?.Data
               ?? files.Read(name);
    };
}
