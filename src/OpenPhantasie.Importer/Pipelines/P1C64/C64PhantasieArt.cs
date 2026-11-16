using System.Text;
using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// The Commodore 64 art of Phantasie I and II, which share one engine (s218 / s219; formats and evidence in
/// <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> and <c>…/phantasie2/GRAPHICS.md</c>): the 80 monsters from their
/// <c>MX n</c> sprite bands, the hero sprites (<c>HSPRIT</c>, one pair per race), the title picture (<c>PIC.*</c>), the
/// town's buildings (<c>FONT+HER</c>'s town character set at the places the town screen prints them), the overworld's
/// character tiles (<c>FONT</c>, the colours and characters of the game's <c>UTIL-3.O</c> map routine) with the town / inn /
/// dungeon and party sprites, the dungeon map's characters, the 14 item / feature pictures, the two character sets and
/// (s221, <see cref="C64EndScreens"/>) the ending and party-wipe pictures.
/// A game gives its map routine's tables and where its files are read (<see cref="P1C64Art"/>, <c>P2C64Art</c>).
/// Colours: <see cref="C64Gfx.Colodore"/>.
/// </summary>
public abstract class C64PhantasieArt
{
    private const int Monsters = 80, MnRecord = 100;

    /// <summary>Races 0..14 in record order; HSPRIT holds one (idle, attacking) pair for each, none for UNDEAD (15).</summary>
    public static readonly string[] Races =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN",
         "LIZARD MAN", "MINOTAUR"];

    /// <summary>The fog class (map bytes from <see cref="FogFrom"/> on).</summary>
    public const int FogClass = 12, FogFrom = 120;

    /// <summary>Overworld tile numbers: b (even x), b + <see cref="OddColumnTile"/> (odd x), then the sprites.</summary>
    public const int OddColumnTile = 256, TownTile = 1000, InnTile = 1001, DungeonTile = 1002;

    /// <summary>The map sprites in FONT+HER (blocks 92–95 at $D700 = file offset 38 + $700): party, town, inn, dungeon.</summary>
    private const int MapSprites = 0x700, DungeonPartySprite = 0x6C0;

    /// <summary>The dungeon's colour (colour RAM over the whole map, live s218) and the unseen cell's character.</summary>
    public const int DungeonColour = 12, UnseenGlyph = 0x20, PartyGlyph = 0xED;
    public static readonly int[] DungeonGlyphs = [0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC];

    /// <summary>
    /// The town screen's six buildings (live s218, the screen of I's TOWNR): text column, row, columns, rows, first
    /// character of FONT+HER's town set ($D800 = file offset 38 + 2048; numbered down each column), colour.
    /// </summary>
    public static readonly (string Name, int Col, int Row, int Cols, int Rows, int First, int Colour)[] Buildings =
    [
        ("guild", 1, 9, 6, 6, 0x40, 7), ("armory", 11, 9, 6, 6, 0x64, 9), ("bank", 21, 9, 6, 6, 0x88, 3),
        ("inn", 1, 17, 6, 6, 0xAC, 15), ("mystic", 21, 17, 6, 6, 0xD0, 12), ("exit", 12, 20, 4, 3, 0xF4, 9),
    ];

    /// <summary>The item / feature pictures (I's FORMAT.md section 2), characters across × down (numbered down each column).</summary>
    public static readonly (string File, int Cols, int Rows)[] ItemPictures =
    [
        ("SHIELD", 7, 7), ("ARMOR", 7, 7), ("WEAPON", 7, 7), ("GOOD WEAPON", 7, 7), ("MG WEAPON", 7, 7),
        ("POTION", 7, 7), ("SCROLL", 7, 7), ("TREASURE", 7, 7), ("GOOD TREASURE", 7, 7),
        ("DOOR", 7, 7), ("TRAP", 7, 7), ("STAIRS", 7, 7), ("LOCK", 7, 7), ("EXIT", 8, 6),
    ];

    /// <summary>
    /// s221: each item / feature picture as written: its file, the PNG's name under <c>items/</c>, the colour the programs
    /// print it in (I's GRAPHICS.md section 7, read in the compiled BASIC) and its pack slot (null: none). A found item's
    /// colour goes by its id (the float test <c>IX = 2</c> etc. hits ids 41, 61 and 81 only), so WEAPON, GOOD WEAPON and MG
    /// WEAPON are written twice. DOOR is loaded by no program: white, a placeholder (its colour [?]), no slot.
    /// </summary>
    public static readonly (string File, string Name, int Colour, string? Slot)[] ItemPictureColours =
    [
        ("SHIELD", "shield", 15, "event.find.shield"), ("ARMOR", "armor", 15, "event.find.armor"),
        ("WEAPON", "weapon", 15, "event.find.weapon"), ("WEAPON", "weapon-yellow", 7, "event.find.weapon-yellow"),
        ("GOOD WEAPON", "good-weapon", 15, "event.find.good-weapon"),
        ("GOOD WEAPON", "good-weapon-light-blue", 14, "event.find.good-weapon-light-blue"),
        ("MG WEAPON", "mg-weapon", 15, "event.find.mg-weapon"), ("MG WEAPON", "mg-weapon-cyan", 3, "event.find.mg-weapon-cyan"),
        ("POTION", "potion", 15, "event.find.potion"), ("SCROLL", "scroll", 7, "event.find.scroll"),
        ("TREASURE", "treasure", 7, "event.find.treasure"), ("GOOD TREASURE", "good-treasure", 7, "event.find.good-treasure"),
        ("DOOR", "door", 1, null), ("TRAP", "trap", 10, "event.trap"), ("STAIRS", "stairs", 12, "event.stairs"),
        ("LOCK", "lock", 9, "event.lock"), ("EXIT", "exit", 5, "event.exit"),
    ];

    /// <summary>
    /// s221: the found item's picture by item id (<c>I$((IT − 1) / 20)</c> in the colour of section 7's id table): first id,
    /// last id, the <see cref="ItemPictureColours"/> name. The same for II (its programs run the same bytecode on its ids).
    /// </summary>
    public static readonly (int First, int Last, string Name)[] FindPictures =
    [
        (1, 20, "shield"), (21, 40, "armor"), (41, 41, "weapon-yellow"), (42, 60, "weapon"),
        (61, 61, "good-weapon-light-blue"), (62, 80, "good-weapon"), (81, 81, "mg-weapon-cyan"), (82, 100, "mg-weapon"),
        (101, 120, "potion"), (121, 140, "scroll"), (141, 160, "treasure"), (161, 180, "good-treasure"),
    ];

    private readonly ImportContext? _ctx;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    protected C64PhantasieArt(ImportContext? ctx) => _ctx = ctx;

    /// <summary>Where the art is written (repo-relative, also the pack's <c>root</c>).</summary>
    public abstract string Root { get; }

    /// <summary>The map routine's colour table (per class: map byte div 10; 12 = fog).</summary>
    public abstract IReadOnlyList<int> ClassColour { get; }

    /// <summary>The map routine's PETSCII table (per class), before the text column's bit 0 is OR'd in.</summary>
    public abstract IReadOnlyList<int> ClassPetscii { get; }

    /// <summary>s221: the game's ending and party-wipe pictures (<see cref="C64EndScreens"/>), paths under <see cref="Root"/>.</summary>
    protected abstract IEnumerable<(string Rel, RgbaImage Image)> EndScreens(Func<string, byte[]?> read);

    /// <summary>Written files (path under <see cref="Root"/> → bytes); also sent to the context when there is one.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>Monster ids with an attacking picture (the one-sprite monsters with a second block).</summary>
    public List<int> Attacking { get; } = [];

    /// <summary>The widest and tallest monster picture (for the combat layout check).</summary>
    public (int W, int H) LargestMonster { get; private set; }

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

    /// <summary>Run every step, reading each file by its directory name.</summary>
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
        foreach (var (rel, img) in EndScreens(read))
            Save(rel, img);
    }

    /// <summary>The monster names (MN field 0, record m = monster m) for the file names.</summary>
    private static string[] MonsterNames(Func<string, byte[]?> read)
    {
        var mn = read("MN") ?? throw new FileNotFoundException("MN not found in the C64 copy", "MN");
        if (mn.Length < Monsters * MnRecord)
            throw new InvalidDataException($"MN: expected at least {Monsters * MnRecord} bytes, got {mn.Length}");
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
            // The attacking frame: the figure's sprite 0 shows the block after the figure's own, j = PartsPerFigure (live: a
            // one-sprite monster's block 0 → 1, I s218 and II s219; II's VIPER, two sprites, blocks 0 1 → 2 1, s219). That
            // every several-sprite monster swaps its sprite 0 is VIPER's rule taken for all [~].
            var j = band.PartsPerFigure;
            if (blocks.Length >= j * C64Gfx.SpriteBlock + 63)
            {
                Save($"monsters/attacking/{file}", band.Figure(blocks, (0, j)));
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

    /// <summary><c>B</c> [C]: PIC.SCR at $8400, PIC.CLR at $D800, PIC.MAP at $A000, multicolour bitmap, background 0.</summary>
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
        foreach (var (file, name, colour, _) in ItemPictureColours)
        {
            var (_, cols, rows) = ItemPictures.Single(p => p.File == file);
            var body = Body(read, file);
            var img = new RgbaImage(cols * 8, rows * 8);
            for (var c = 0; c < cols; c++)
            for (var r = 0; r < rows; r++)
                C64Gfx.DrawChar(img, c * 8, r * 8, body.AsSpan((c * rows + r) * 8, 8), colour, null);
            Save($"items/{name}.png", img);
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
