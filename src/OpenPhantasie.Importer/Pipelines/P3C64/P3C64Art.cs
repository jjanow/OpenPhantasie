using System.Text;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1C64;

namespace OpenPhantasie.Importer.Pipelines.P3C64;

/// <summary>
/// The Commodore 64 art of Phantasie III (s220; formats and evidence in <c>reverse-engineering/c64/phantasie3/GRAPHICS.md</c>).
/// Not I / II's engine: III's C64 is 6502 machine code (<c>PHANT.OBJ</c> runs at file address − $1000) with its own
/// pictures. From the files: the title (<c>TITLE</c>, a Koala multicolour bitmap), the 80 monsters (<c>MONSTERS</c>'
/// sprite blocks, picked by record byte 27, coloured by <c>PHANT.OBJ</c>'s tables), the party figures
/// (<c>HEROSPRITES</c>, 18 sets of three frames picked by class or race), the three ending pictures (<c>GUT</c>,
/// <c>SCHLECHT</c>, <c>TOT</c>: a multicolour character set with its screen), the overworld's and the dungeon's
/// character tiles (<c>PSET0</c>), the town (<c>PTOWN1</c>) and the character sets as sheets.
/// Colours: <see cref="C64Gfx.Colodore"/>. Then (<see cref="Import"/>) the pack <c>GameData/phantasie3/graphics/c64.json</c>
/// (<see cref="P3C64ArtManifest"/>).
/// </summary>
public sealed class P3C64Art(ImportContext? ctx)
{
    public const string GameId = "phantasie3";
    public const string RootPath = "reverse-engineering/c64/phantasie3/extracted/graphics";

    private const int Monsters = 80, MonsterRecord = 38, MonsterRecords = 4992, PictureByte = 27;

    /// <summary>MONSTERS' sprite blocks sit at VIC block 128 on ($E000 in bank 3): record byte 27 = 128 + the file's block.</summary>
    public const int FirstBlock = 128;

    /// <summary>The two-sprite (giant) pictures: block b over b + 1, attacking b + 2 over b + 3 (PHANT.OBJ $2836) [C].</summary>
    public static bool IsGiant(int picture) => picture == 0xA8 || picture is >= 0xBA and < 0xCE;

    /// <summary>The giant whose lower sprite is hires whatever its row's mask (PHANT.OBJ $2853: <c>$490C</c> = 0) [C].</summary>
    public const int HiresLowerHalf = 0xCA;

    /// <summary>Races 0..15 in the C64's (and Core's) numbering, the race words of PHANT.OBJ.</summary>
    public static readonly string[] Races =
        ["HUMAN", "DWARF", "ELF", "HALFLING", "GNOME", "PIXIE", "SPRITE", "OGRE", "GNOLL", "TROLL", "KOBOLD", "ORC", "GOBLIN",
         "LIZARD MAN", "MINOTAUR", "UNDEAD"];

    /// <summary>Classes in Core's (and the C64's) order.</summary>
    public static readonly string[] Classes = ["THIEF", "MONK", "RANGER", "FIGHTER", "PRIEST", "WIZARD"];

    /// <summary>The classes that have their own figure (record byte 169 not 0 or 3: PHANT.OBJ $1D98) [C].</summary>
    public static readonly int[] ClassFigures = [1, 2, 4, 5];

    /// <summary>The three elementals (roster slots 18–20, ELEMENTALS' order).</summary>
    public static readonly string[] Elementals = ["AIR", "MIST", "MUD"];

    /// <summary>The ending pictures: file, the remaster's slot word, <c>$D022</c> / <c>$D023</c> (DUNGEON.OBJ $5D7D / $5D80) [C].</summary>
    public static readonly (string File, string Name, int Mc1, int Mc2)[] Endings =
        [("TOT", "astral-plane", 8, 9), ("GUT", "reward-good", 14, 10), ("SCHLECHT", "reward-evil", 11, 8)];

    /// <summary>
    /// PHANT.OBJ's tables (file offsets after the load address = runtime − $0400) in each build: monster colours
    /// (runtime $37C0: low nibble <c>$D025</c>, high nibble <c>$D026</c>), monster sprite colour (runtime $3810: bit 7 =
    /// hires), the figure tables (runtime $1DF2: per class figure set, per class colour, per race set, per race colour, 6 +
    /// 6 + 16 + 16 bytes) and the elementals' colours (runtime $5887).
    /// </summary>
    public sealed record Build(string Name, int MonsterMulticolour, int MonsterColour, int Figures, int ElementalColour);

    public static Build BuildOf(byte[] phant) => phant.Length switch
    {
        22547 => new("v1.0", 0x33C0, 0x3410, 0x19F2, 0x5487),
        22553 => new("v1.2 (alt)", 0x33C2, 0x3412, 0x19F2, 0x5489),
        _ => throw new InvalidDataException($"PHANT.OBJ: {phant.Length} bytes, not a known build"),
    };

    /// <summary>The import step: the art under <see cref="RootPath"/> and the pack <c>graphics/c64.json</c>.</summary>
    public static void Import(ImportContext context)
    {
        new P3C64Art(context).Run(name => context.Files.Find(name)?.Data);
        context.WriteGameDataBytes(GameId, "graphics/c64.json", PyJson.SerializeUtf8(P3C64ArtManifest.Pack(), 2, ensureAscii: true));
    }

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>Written files (path under <see cref="RootPath"/> → bytes); also sent to the context when there is one.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>Monster id → its picture byte (record byte 27).</summary>
    public int[] PictureOf { get; } = new int[Monsters];

    /// <summary>The widest and tallest monster picture (for the combat layout check).</summary>
    public (int W, int H) LargestMonster { get; private set; }

    public Build? Version { get; private set; }

    private void Save(string rel, RgbaImage img)
    {
        var bytes = Png.Encode(img, forceAlpha: true);
        _files[rel] = bytes;
        ctx?.WriteFile($"{RootPath}/{rel}", bytes);
    }

    private static byte[] Body(Func<string, byte[]?> read, string name)
    {
        var prg = read(name) ?? throw new FileNotFoundException($"{name} not found in the C64 copy", name);
        if (prg.Length < 2)
            throw new InvalidDataException($"{name}: a PRG file without a load address");
        return prg[2..];
    }

    private static string Slug(string name) => C64PhantasieArt.Slug(name);

    /// <summary>Run every step, reading each file by its directory name.</summary>
    public void Run(Func<string, byte[]?> read)
    {
        var phant = Body(read, "PHANT.OBJ");
        Version = BuildOf(phant);
        Title(read);
        Monsters_(read, phant);
        Figures(read, phant);
        EndingPictures(read);
        Overworld(read);
        Dungeon(read);
        Town(read);
        Charsets(read);
    }

    /// <summary><c>TITLE</c>: a Koala picture (bitmap 8000, screen 1000, colour 1000, background 1), loaded at $6000 [C].</summary>
    private void Title(Func<string, byte[]?> read)
    {
        var t = Body(read, "TITLE");
        if (t.Length < 10001)
            throw new InvalidDataException($"TITLE: {t.Length} bytes, not a Koala picture");
        Save("screens/title.png", C64Gfx.MulticolourBitmap(t.AsSpan(0, 8000), t.AsSpan(8000, 1000), t.AsSpan(9000, 1000), t[10000] & 15));
    }

    private static string MonsterName(byte[] mon, int m)
    {
        var rec = mon.AsSpan(MonsterRecords + m * MonsterRecord, 14);
        var end = rec.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? rec : rec[..end]).Trim();
    }

    private void Monsters_(Func<string, byte[]?> read, byte[] phant)
    {
        var mon = Body(read, "MONSTERS");
        var town = Body(read, "PTOWN1");
        if (mon.Length < MonsterRecords + Monsters * MonsterRecord)
            throw new InvalidDataException($"MONSTERS: {mon.Length} bytes");
        var v = Version!;
        int w = 0, h = 0;
        for (var m = 0; m < Monsters; m++)
        {
            var picture = mon[MonsterRecords + m * MonsterRecord + PictureByte];
            PictureOf[m] = picture;
            var file = $"{m:D2}-{Slug(MonsterName(mon, m))}.png";
            int mcs = phant[v.MonsterMulticolour + m], colour = phant[v.MonsterColour + m];
            var multi = (colour & 0x80) == 0;
            RgbaImage Figure(int b)
            {
                var giant = IsGiant(picture);
                var img = new RgbaImage(C64Gfx.SpriteWidth, C64Gfx.SpriteHeight * (giant ? 2 : 1));
                Draw(img, 0, b, multi);
                if (giant)
                    Draw(img, C64Gfx.SpriteHeight, b + 1, multi && picture != HiresLowerHalf);
                return img;
            }
            void Draw(RgbaImage img, int y, int b, bool mc)
            {
                var data = Block(mon, town, b);
                if (mc)
                    C64Gfx.DrawMulticolourSprite(img, 0, y, data, colour & 15, mcs & 15, mcs >> 4);
                else
                    C64Gfx.DrawSprite(img, 0, y, data, colour & 15);
            }
            var idle = Figure(picture);
            Save($"monsters/idle/{file}", idle);
            // Attacking: the pointer + 1, + 2 for a giant (PHANT.OBJ $3060: SEC, ADC $3059) [C].
            Save($"monsters/attacking/{file}", Figure(picture + (IsGiant(picture) ? 2 : 1)));
            w = Math.Max(w, idle.Width);
            h = Math.Max(h, idle.Height);
        }
        LargestMonster = (w, h);
    }

    /// <summary>
    /// The 63 bytes of VIC sprite block <paramref name="block"/> in a fight: 128 on are MONSTERS' blocks at $E000 (live
    /// s220: RAM equals the file); 126 and 127 ($DF80, Boo Boo Kitty's) are the last 128 bytes of PTOWN1, the town
    /// character set that stays at $D800 (live s220) [C].
    /// </summary>
    private static ReadOnlySpan<byte> Block(byte[] mon, byte[] town, int block) => block >= FirstBlock
        ? mon.AsSpan((block - FirstBlock) * C64Gfx.SpriteBlock, 63)
        : town.AsSpan(2048 - (FirstBlock - block) * C64Gfx.SpriteBlock, 63);

    /// <summary>
    /// <c>HEROSPRITES</c>: 18 sets of three blocks (idle, attacking, casting). A member's set (PHANT.OBJ $1D94): a Monk,
    /// Ranger, Priest or Wizard takes its class's set and colour, a Thief or Fighter its race's (hires, in the party row's
    /// band) [C]. The elemental (party slot 7, PHANT.OBJ $5868) shows the Mud Elemental's monster picture ($B2) in its own
    /// colour [C].
    /// </summary>
    private void Figures(Func<string, byte[]?> read, byte[] phant)
    {
        var heroes = Body(read, "HEROSPRITES");
        var at = Version!.Figures;
        int ClassSet(int c) => phant[at + c];
        int ClassColour(int c) => phant[at + 6 + c];
        int RaceSet(int r) => phant[at + 12 + r];
        int RaceColour(int r) => phant[at + 28 + r];
        string[] poses = ["idle", "attacking", "casting"];
        void Set(string dir, string stem, int set, int colour)
        {
            for (var f = 0; f < poses.Length; f++)
                Save($"{dir}/{poses[f]}/{stem}.png", C64Gfx.Sprite(heroes.AsSpan((3 * set + f) * C64Gfx.SpriteBlock, 63), colour & 15));
        }
        for (var r = 0; r < Races.Length; r++)
            Set("portraits", $"{r:D2}-{Slug(Races[r])}", RaceSet(r), RaceColour(r));
        foreach (var c in ClassFigures)
            Set("portraits/class", $"{c}-{Slug(Classes[c])}", ClassSet(c), ClassColour(c));

        var mon = Body(read, "MONSTERS");
        const int elementalBlock = 0xB2 - FirstBlock;
        for (var e = 0; e < Elementals.Length; e++)
        {
            var colour = phant[Version.ElementalColour + e] & 15;
            Save($"portraits/elemental/{Slug(Elementals[e])}-idle.png", C64Gfx.Sprite(mon.AsSpan(elementalBlock * C64Gfx.SpriteBlock, 63), colour));
            Save($"portraits/elemental/{Slug(Elementals[e])}-attacking.png",
                C64Gfx.Sprite(mon.AsSpan((elementalBlock + 1) * C64Gfx.SpriteBlock, 63), colour));
        }
    }

    /// <summary>
    /// <c>GUT</c>, <c>SCHLECHT</c>, <c>TOT</c> (4,096 bytes at $8000): a 2,048-byte multicolour character set, 1,000 screen
    /// codes, 24 spare bytes, 1,000 colour-RAM bytes; background 0 and the two shared colours from DUNGEON.OBJ (rendered
    /// whole: Zeus, Pluto, the demon) [C].
    /// </summary>
    private void EndingPictures(Func<string, byte[]?> read)
    {
        foreach (var (file, name, mc1, mc2) in Endings)
        {
            var d = Body(read, file);
            if (d.Length < 4072)
                throw new InvalidDataException($"{file}: {d.Length} bytes");
            Save($"screens/{name}.png", C64Gfx.MulticolourCharScreen(d.AsSpan(0, 2048), d.AsSpan(2048, 1000), d.AsSpan(3072, 1000), 0, mc1, mc2));
        }
    }

    /// <summary>
    /// The wilderness map (PHANT.OBJ's view, live s220): each map cell is 2 × 2 characters of PSET0 in multicolour, the
    /// characters and colour-RAM nibbles from DATA's four 256-byte tables each (top left, top right, bottom left, bottom
    /// right; file offset <see cref="TileChars"/> and <see cref="TileColours"/>, indexed by the map byte), background 0,
    /// <c>$D022</c> 1, <c>$D023</c> 5 (the map band's raster interrupt, PHANT.OBJ $04E0) [C]. The party replaces its cell
    /// with characters $71 $74 $73 $72 in colour-RAM 11 [C].
    /// </summary>
    private void Overworld(Func<string, byte[]?> read)
    {
        var data = Body(read, "DATA");
        var set = Body(read, "PSET0");
        RgbaImage Quad(Func<int, int> ch, Func<int, int> colour)
        {
            var img = new RgbaImage(16, 16);
            for (var k = 0; k < 4; k++)
                C64Gfx.DrawMulticolourChar(img, (k & 1) * 8, (k >> 1) * 8, set.AsSpan(ch(k) * 8, 8), colour(k), 0, MapMc1, MapMc2);
            return img;
        }
        for (var t = 0; t < 256; t++)
            Save($"overworld/tiles/{t:D3}.png", Quad(k => data[TileChars + 256 * k + t], k => data[TileColours + 256 * k + t]));
        Save("overworld/party.png", Quad(k => PartyChars[k], _ => PartyColour));
    }

    /// <summary>DATA's tile tables (file offsets after the load address; DATA loads at $2000) [C].</summary>
    public const int TileChars = 0x100, TileColours = 0x500;

    /// <summary>The map band's shared colours and the party's cell [C].</summary>
    public const int MapMc1 = 1, MapMc2 = 5, PartyColour = 11;
    public static readonly int[] PartyChars = [0x71, 0x74, 0x73, 0x72];

    /// <summary>
    /// The dungeon map (DUNGEON.OBJ, live s220): a 144 × 105 multicolour sprite bitmap (6 × 5 sprites, blocks 0–29, all in
    /// colour 5 with <c>$D025</c> 7 and <c>$D026</c> 12) over black, first filled grey; the level's 24 × 30 cells are 3 × 3
    /// multicolour pixels (6 × 3 screen pixels) at pixel-pair column 3x, row 3y + 3, the whole level at once. The game
    /// copies a cell's glyph from DUNGEON.OBJ's table at runtime $6E77 (file offset <see cref="DungeonGlyphTable"/>): 32
    /// bytes a glyph, four shift phases of 8 bytes (rows of 16 bits, high byte first); phase 0's top three rows are the
    /// glyph [C]. Glyphs: 0 blank, 1 grey (wall and not yet seen), 2 a dot, 3 an upright yellow bar, 4 a level one, 5
    /// the exit's arch, 6 the party.
    /// </summary>
    private void Dungeon(Func<string, byte[]?> read)
    {
        var dng = Body(read, "DUNGEON.OBJ");
        for (var g = 0; g < DungeonGlyphs; g++)
        {
            var img = new RgbaImage(DungeonGlyphSize.W, DungeonGlyphSize.H);
            for (var row = 0; row < DungeonGlyphSize.H; row++)
            {
                var at = DungeonGlyphTable + 32 * g + 2 * row;
                var word = dng[at] << 8 | dng[at + 1];
                for (var pair = 0; pair < 3; pair++)
                {
                    var c = ((word >> (14 - 2 * pair)) & 3) switch { 0 => 0, 1 => 7, 2 => 5, _ => 12 };
                    C64Gfx.Plot(img, 2 * pair, row, c);
                    C64Gfx.Plot(img, 2 * pair + 1, row, c);
                }
            }
            Save($"dungeon/glyph-{g}.png", img);
        }
    }

    /// <summary>DUNGEON.OBJ's glyph table (runtime $6E77 = file offset + $5C20; the same file in v1.0 and v1.2).</summary>
    public const int DungeonGlyphTable = 0x1257, DungeonGlyphs = 7;
    public static readonly (int W, int H) DungeonGlyphSize = (6, 3);
    public const int DungeonUnseen = 1, DungeonParty = 6, DungeonDefault = 0;

    /// <summary>
    /// Cell byte → glyph: 0 / 1 grey (a seen wall looks unexplored), 200–214 the upright bar (doors, seen s220 on bytes
    /// 200 and 208), 215–217 the exit (seen on 215); 185–199 the level bar and 221+ the dot by DOS's ranges (ovl4 0x21bb)
    /// [~]; the rest (2–184 seen as 17 and 141, 218–220) blank. The draw routine's own test is not read.
    /// </summary>
    public static readonly int[][] DungeonCells =
        [[0, 1, 1], [185, 199, 4], [200, 214, 3], [215, 217, 5], [221, 255, 2]];

    public const string DungeonNote = "Dungeon (DUNGEON.OBJ, live s220): the whole 24 x 30 level in 6 x 3-pixel cells " +
        "(3 x 3 multicolour sprite pixels; sprite colour 5, $D025 7, $D026 12, black ground), glyphs from the table at " +
        "runtime $6E77: 0 blank, 1 grey (walls and cells not yet seen), 2 dot, 3 upright bar, 4 level bar, 5 exit, 6 party " +
        "(drawn instead of its cell). Bytes 200-214 (doors) and 215 seen live; 185-199 and 221+ by DOS's ranges [~]. The " +
        "upright bar's fourth row, which the C64 draws into the next cell's top row, is left out.";

    /// <summary>
    /// The town screen's picture (live s220): text rows 0–2 print characters $40–$B7 of PTOWN1 (at $D800) in reading
    /// order, multicolour, background 0, <c>$D022</c> 15, <c>$D023</c> 9, the colour-RAM nibbles from PHANT.OBJ's 120
    /// bytes at runtime $055D [C]; row 3 prints the buildings' names (GUILD ARMORY MYSTIC BANK INN LEAVE TOWN), which the
    /// Client's own bar replaces. The pack's backdrop puts the strip at <see cref="TownStripY"/> on black, below the
    /// Client's bar.
    /// </summary>
    private void Town(Func<string, byte[]?> read)
    {
        var set = Body(read, "PTOWN1");
        var phant = Body(read, "PHANT.OBJ");
        var img = new RgbaImage(320, 200);
        for (var i = 0; i < img.Pixels.Length; i += 4)
            img.Pixels[i + 3] = 255;
        for (var cell = 0; cell < 120; cell++)
            C64Gfx.DrawMulticolourChar(img, cell % 40 * 8, TownStripY + cell / 40 * 8, set.AsSpan((0x40 + cell) * 8, 8),
                phant[TownColours + cell], 0, 15, 9);
        Save("screens/town.png", img);
    }

    /// <summary>PHANT.OBJ's town colour bytes (runtime $055D; the same in both builds) and where the strip sits.</summary>
    public const int TownColours = 0x15D, TownStripY = 16;

    /// <summary>The character sets as sheets (16 × 16 characters, white on black, hires bits): PSET0 and PTOWN1.</summary>
    private void Charsets(Func<string, byte[]?> read)
    {
        foreach (var name in new[] { "PSET0", "PTOWN1" })
        {
            var set = Body(read, name);
            var img = new RgbaImage(128, 128);
            for (var ch = 0; ch < 256; ch++)
                C64Gfx.DrawChar(img, ch % 16 * 8, ch / 16 * 8, set.AsSpan(ch * 8, 8), 1, 0);
            Save($"font/{Slug(name)}.png", img);
        }
    }
}
