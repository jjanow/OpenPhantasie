using System.Text;
using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// The Commodore 64 art of Phantasie I (s218; formats and evidence in <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>):
/// the 80 monsters from their <c>MX n</c> sprite bands, the hero sprites (<c>HSPRIT</c>, one pair per race), the title
/// picture (<c>PIC.*</c>), the town's buildings (<c>FONT+HER</c>'s town character set at the places the town screen
/// prints them), the overworld's character tiles (<c>FONT</c>, the colours and characters of <c>UTIL-3.O</c>'s map
/// routine) with the town / inn / dungeon and party sprites, the dungeon map's characters, the 14 item / feature
/// pictures and the two character sets; then (<see cref="Import"/>) the pack <c>GameData/phantasie1/graphics/c64.json</c>
/// (<see cref="P1C64ArtManifest"/>). Colours: <see cref="C64Gfx.Colodore"/>.
/// </summary>
public sealed class P1C64Art
{
    public const string GameId = "phantasie1";
    public const string Root = "reverse-engineering/c64/phantasie1/extracted/graphics";

    private const int Monsters = 80, MnRecord = 100;

    /// <summary>Races 0..14 in record order; HSPRIT holds one (idle, attacking) pair for each, none for UNDEAD (15).</summary>
    public static readonly string[] Races =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN",
         "LIZARD MAN", "MINOTAUR"];

    /// <summary>
    /// <c>UTIL-3.O</c>'s map routine (<c>$C5C3</c>): per class (map byte div 10; 12 = fog, bytes 120+) the colour
    /// (<c>$C688</c>) and the PETSCII character (<c>$C695</c>) it prints [C].
    /// </summary>
    public static readonly int[] ClassColour = [5, 5, 9, 9, 0, 0, 11, 11, 9, 9, 6, 6, 0];
    public static readonly int[] ClassPetscii = [0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x20, 0x20];
    public const int FogClass = 12, FogFrom = 120;

    /// <summary>Overworld tile numbers: b (even x), b + <see cref="OddColumnTile"/> (odd x), then the sprites.</summary>
    public const int OddColumnTile = 256, TownTile = 1000, InnTile = 1001, DungeonTile = 1002;

    /// <summary>The map sprites in FONT+HER (blocks 92–95 at $D700 = file offset 38 + $700): party, town, inn, dungeon.</summary>
    private const int MapSprites = 0x700, DungeonPartySprite = 0x6C0;

    /// <summary>The dungeon's colour (colour RAM over the whole map, live s218) and the unseen cell's character.</summary>
    public const int DungeonColour = 12, UnseenGlyph = 0x20, PartyGlyph = 0xED;
    public static readonly int[] DungeonGlyphs = [0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC];

    /// <summary>
    /// The town screen's six buildings (live s218, the screen of TOWNR): text column, row, columns, rows, first character of
    /// FONT+HER's town set ($D800 = file offset 38 + 2048; numbered down each column), colour.
    /// </summary>
    public static readonly (string Name, int Col, int Row, int Cols, int Rows, int First, int Colour)[] Buildings =
    [
        ("guild", 1, 9, 6, 6, 0x40, 7), ("armory", 11, 9, 6, 6, 0x64, 9), ("bank", 21, 9, 6, 6, 0x88, 3),
        ("inn", 1, 17, 6, 6, 0xAC, 15), ("mystic", 21, 17, 6, 6, 0xD0, 12), ("exit", 12, 20, 4, 3, 0xF4, 9),
    ];

    /// <summary>The item / feature pictures (FORMAT.md section 2), side, characters across × down (numbered down each column).</summary>
    public static readonly (string File, int Side, int Cols, int Rows)[] ItemPictures =
    [
        ("SHIELD", 1, 7, 7), ("ARMOR", 1, 7, 7), ("WEAPON", 1, 7, 7), ("GOOD WEAPON", 1, 7, 7), ("MG WEAPON", 1, 7, 7),
        ("POTION", 1, 7, 7), ("SCROLL", 1, 7, 7), ("TREASURE", 1, 7, 7), ("GOOD TREASURE", 1, 7, 7),
        ("DOOR", 2, 7, 7), ("TRAP", 2, 7, 7), ("STAIRS", 2, 7, 7), ("LOCK", 2, 7, 7), ("EXIT", 2, 8, 6),
    ];

    private readonly ImportContext? _ctx;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public P1C64Art(ImportContext? ctx) => _ctx = ctx;

    /// <summary>Written files (path under <see cref="Root"/> → bytes); also sent to the context when there is one.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>Monster ids with an attacking picture (the one-sprite monsters with a second block).</summary>
    public List<int> Attacking { get; } = [];

    /// <summary>The widest and tallest monster picture (for the combat layout check).</summary>
    public (int W, int H) LargestMonster { get; private set; }

    /// <summary>The import step: the art under <see cref="Root"/> and the pack <c>graphics/c64.json</c>.</summary>
    public static void Import(ImportContext context)
    {
        var art = new P1C64Art(context);
        art.Run(name => context.Files.Find(name)?.Data);
        context.WriteGameDataBytes(GameId, "graphics/c64.json", PyJson.SerializeUtf8(P1C64ArtManifest.Pack(), 2, ensureAscii: true));
    }

    private void Save(string rel, RgbaImage img)
    {
        var bytes = Png.Encode(img, forceAlpha: true);
        _files[rel] = bytes;
        _ctx?.WriteFile($"{Root}/{rel}", bytes);
    }

    private static byte[] Body(Func<string, byte[]?> read, string name)
    {
        var prg = read(name) ?? throw new FileNotFoundException($"{name} not found in the C64 copy", name);
        if (prg.Length < 2)
            throw new InvalidDataException($"{name}: a PRG file without a load address");
        return prg[2..];
    }

    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    /// <summary>Run every step from the files of both disk sides (by directory name).</summary>
    public void Run(Func<string, byte[]?> read)
    {
        Monsters_(read);
        Heroes(read);
        Title(read);
        var fontHer = Body(read, "FONT+HER");
        var font = Body(read, "FONT");
        Town(fontHer);
        Overworld(font, fontHer);
        Dungeon(font, fontHer);
        Items(read);
        Charsets(font, fontHer);
    }

    /// <summary>The monster names (MN field 0, record m = monster m) for the file names.</summary>
    private static string[] MonsterNames(Func<string, byte[]?> read)
    {
        var mn = read("MN") ?? throw new FileNotFoundException("MN not found in the C64 copy", "MN");
        var names = new string[Monsters];
        for (var m = 0; m < Monsters; m++)
        {
            var rec = mn.AsSpan(m * MnRecord, MnRecord);
            var end = rec.IndexOf((byte)13);
            names[m] = Encoding.ASCII.GetString(rec[..(end < 0 ? 0 : end)]).Trim();
        }
        return names;
    }

    private void Monsters_(Func<string, byte[]?> read)
    {
        var names = MonsterNames(read);
        int w = 0, h = 0;
        for (var m = 0; m < Monsters; m++)
        {
            var body = Body(read, $"MX {m + 1}");
            var band = SpriteBand.Parse(body);
            var blocks = body.AsSpan(SpriteBand.Size);
            var file = $"{m:D2}-{Slug(names[m])}.png";
            var idle = band.Figure(blocks);
            Save($"monsters/idle/{file}", idle);
            w = Math.Max(w, idle.Width);
            h = Math.Max(h, idle.Height);
            // The attacking frame (live s218): a one-sprite monster's band shows block 1 while it attacks. The extra block of
            // a several-sprite monster is not placed [?]: no attacking picture, the idle one stands in.
            if (band.PartsPerFigure == 1 && blocks.Length >= 2 * C64Gfx.SpriteBlock - 1)
            {
                Save($"monsters/attacking/{file}", band.Figure(blocks, b => b + 1));
                Attacking.Add(m);
            }
        }
        LargestMonster = (w, h);
    }

    private void Heroes(Func<string, byte[]?> read)
    {
        var hsprit = Body(read, "HSPRIT");
        for (var r = 0; r < Races.Length; r++)
        {
            var file = $"{r:D2}-{Slug(Races[r])}.png";
            // Yellow: the combat row's colour (FONT+HER's band at run time, live s218) [~].
            Save($"portraits/idle/{file}", C64Gfx.Sprite(hsprit.AsSpan(2 * r * 64, 63), 7));
            Save($"portraits/attacking/{file}", C64Gfx.Sprite(hsprit.AsSpan((2 * r + 1) * 64, 63), 7));
        }
    }

    /// <summary><c>B</c> lines 200–290 [C]: PIC.SCR at $8400, PIC.CLR at $D800, PIC.MAP at $A000, multicolour bitmap, background 0 (line 595).</summary>
    private void Title(Func<string, byte[]?> read)
    {
        var map = Body(read, "PIC.MAP");
        var scr = Body(read, "PIC.SCR");
        var clr = Body(read, "PIC.CLR");
        Save("screens/title.png", C64Gfx.MulticolourBitmap(map.AsSpan(0, 8000), scr.AsSpan(0, 1000), clr.AsSpan(0, 1000), 0));
    }

    private static ReadOnlySpan<byte> TownSet(byte[] fontHer) => fontHer.AsSpan(SpriteBand.Size + 2048, 2048);

    private void Town(byte[] fontHer)
    {
        var set = TownSet(fontHer);
        var img = new RgbaImage(320, 200);
        for (var i = 0; i < img.Pixels.Length; i += 4)
            img.Pixels[i + 3] = 255; // black
        foreach (var b in Buildings)
            for (var c = 0; c < b.Cols; c++)
            for (var r = 0; r < b.Rows; r++)
            {
                var ch = b.First + c * b.Rows + r;
                C64Gfx.DrawChar(img, (b.Col + c) * 8, (b.Row + r) * 8, set.Slice(ch * 8, 8), b.Colour, 0);
            }
        Save("screens/town.png", img);
    }

    /// <summary>Screen code of a PETSCII character the map routine prints ($60–$7F → $40–$5F).</summary>
    private static int ScreenCode(int petscii) => petscii is >= 0x60 and <= 0x7F ? petscii - 0x20 : petscii;

    private void Overworld(byte[] font, byte[] fontHer)
    {
        for (var c = 0; c <= FogClass; c++)
        {
            var name = c == FogClass ? "fog" : $"class-{c:D2}";
            var code = ScreenCode(ClassPetscii[c]);
            // The routine ORs the text column's bit 0 in: the even map columns (odd text columns) print code + 1.
            Save($"overworld/tiles/{name}-even.png", C64Gfx.Char(font.AsSpan((code | 1) * 8, 8), ClassColour[c], 0));
            Save($"overworld/tiles/{name}-odd.png", C64Gfx.Char(font.AsSpan(code * 8, 8), ClassColour[c], 0));
        }
        var sprites = fontHer.AsSpan(SpriteBand.Size + MapSprites);
        Save("overworld/party.png", C64Gfx.Sprite(sprites[..63], 1));
        Save("overworld/town.png", C64Gfx.Sprite(sprites.Slice(64, 63), 7));
        Save("overworld/inn.png", C64Gfx.Sprite(sprites.Slice(128, 63), 7));
        Save("overworld/dungeon.png", C64Gfx.Sprite(sprites.Slice(192, 63), 7));
    }

    private void Dungeon(byte[] font, byte[] fontHer)
    {
        foreach (var g in DungeonGlyphs.Append(UnseenGlyph))
            Save($"dungeon/glyph-{g}.png", C64Gfx.Char(font.AsSpan(g * 8, 8), DungeonColour, 0));
        // The party: sprite block 91 ($D6C0) in yellow over the cell; its pixels fit the cell's 8 × 8 (checked).
        var party = C64Gfx.Sprite(fontHer.AsSpan(SpriteBand.Size + DungeonPartySprite, 63), 7);
        if (C64Gfx.Bounds(party) is not { } box || box.X + box.W > 8 || box.Y + box.H > 8)
            throw new InvalidDataException("FONT+HER: the dungeon party sprite does not fit one 8 × 8 cell");
        Save($"dungeon/glyph-{PartyGlyph}.png", C64Gfx.Crop(party, 0, 0, 8, 8));
    }

    private void Items(Func<string, byte[]?> read)
    {
        foreach (var (file, _, cols, rows) in ItemPictures)
        {
            var body = Body(read, file);
            var img = new RgbaImage(cols * 8, rows * 8);
            // The colour each is shown in is set by the programs: EXIT green (5, live s218); the others are not read [?] (white).
            var colour = file == "EXIT" ? 5 : 1;
            for (var c = 0; c < cols; c++)
            for (var r = 0; r < rows; r++)
                C64Gfx.DrawChar(img, c * 8, r * 8, body.AsSpan((c * rows + r) * 8, 8), colour, null);
            Save($"items/{Slug(file)}.png", img);
        }
    }

    /// <summary>The character sets as sheets (16 × 16 characters, white on black): FONT ($E800) and FONT+HER's two ($D000, $D800).</summary>
    private void Charsets(byte[] font, byte[] fontHer)
    {
        void Sheet(string rel, ReadOnlySpan<byte> set)
        {
            var img = new RgbaImage(128, 128);
            for (var ch = 0; ch < 256; ch++)
                C64Gfx.DrawChar(img, ch % 16 * 8, ch / 16 * 8, set.Slice(ch * 8, 8), 1, 0);
            Save(rel, img);
        }
        Sheet("font/font-e800.png", font.AsSpan(0, 2048));
        Sheet("font/font-her-d000.png", fontHer.AsSpan(SpriteBand.Size, 2048));
        Sheet("font/font-her-d800-town.png", TownSet(fontHer));
    }
}
