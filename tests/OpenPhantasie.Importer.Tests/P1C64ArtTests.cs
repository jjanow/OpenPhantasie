using System.Security.Cryptography;
using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1C64;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// s218: Phantasie I's C64 art (<see cref="P1C64Art"/>, <see cref="C64Gfx"/>; notes
/// <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>). The decoders on made-up bytes; then, from the C64PP v1.4 pair,
/// the art equals the committed extract, a few pictures' pixels equal fixed checksums, and the pack equals the shipped
/// <c>c64.json</c>.
/// </summary>
public class P1C64ArtTests
{
    private const string Images = "reverse-engineering/c64/original-images/phantasie1/c64pp/";
    private const string Side1 = Images + "phantasie_s1[ssi_1985](v1_4).g64";
    private const string Side2 = Images + "phantasie_s2[ssi_1985](v1_4).g64";
    private const string ArtRoot = P1C64Art.RootPath;
    private const string Shipped = "src/OpenPhantasie.Data/GameData/phantasie1/graphics/c64.json";

    private static Func<string, byte[]?> Lookup()
    {
        var sides = new[] { Side1, Side2 }.Select(p =>
        {
            var raw = File.ReadAllBytes(Repo.Path(p));
            var vol = D64Volume.TryOpen(G64Image.IsG64(raw) ? G64Image.Decode(raw).Image : raw) ?? throw new InvalidDataException(p);
            return vol;
        }).ToList();
        return name => sides.Select(v => v.Files.FirstOrDefault(e => e.Name == name) is { } e ? v.Read(e) : null)
            .FirstOrDefault(d => d is not null);
    }

    private static readonly Lazy<P1C64Art> Art = new(() =>
    {
        var art = new P1C64Art(null);
        art.Run(Lookup());
        return art;
    });

    private static (byte R, byte G, byte B, byte A) Px(RgbaImage img, int x, int y)
    {
        var i = (y * img.Width + x) * 4;
        return (img.Pixels[i], img.Pixels[i + 1], img.Pixels[i + 2], img.Pixels[i + 3]);
    }

    private static (byte, byte, byte, byte) Vic(int c) => (C64Gfx.Colodore[c].R, C64Gfx.Colodore[c].G, C64Gfx.Colodore[c].B, 255);

    [Fact]
    public void Sprite_DrawsSetBits_AndDoublesExpandedAxes()
    {
        var data = new byte[63];
        data[0] = 0x80;          // (0, 0)
        data[3 * 20 + 2] = 0x01; // (23, 20)
        var plain = C64Gfx.Sprite(data, 2);
        Assert.Equal((24, 21), (plain.Width, plain.Height));
        Assert.Equal(Vic(2), Px(plain, 0, 0));
        Assert.Equal(Vic(2), Px(plain, 23, 20));
        Assert.Equal(0, Px(plain, 1, 0).Item4); // clear bits stay transparent

        var big = new RgbaImage(48, 42);
        C64Gfx.DrawSprite(big, 0, 0, data, 7, xExpand: true, yExpand: true);
        foreach (var (x, y) in new[] { (0, 0), (1, 0), (0, 1), (1, 1), (46, 40), (47, 41) })
            Assert.Equal(Vic(7), Px(big, x, y));
        Assert.Equal(0, Px(big, 2, 0).Item4);
    }

    [Fact]
    public void MulticolourBitmap_PicksBackground_ScreenNibbles_AndColourRam()
    {
        var bitmap = new byte[8000];
        bitmap[0] = 0b00_01_10_11;
        var screen = new byte[1000];
        screen[0] = 0x2E; // 01 → 2 (red), 10 → 14 (light blue)
        var colour = new byte[1000];
        colour[0] = 0xF5; // 11 → 5 (green): only the low nibble counts
        var img = C64Gfx.MulticolourBitmap(bitmap, screen, colour, 6);
        Assert.Equal((320, 200), (img.Width, img.Height));
        Assert.Equal([Vic(6), Vic(6), Vic(2), Vic(2), Vic(14), Vic(14), Vic(5), Vic(5)],
            Enumerable.Range(0, 8).Select(x => Px(img, x, 0)));
    }

    [Fact]
    public void SpriteBand_ParsesTheRecord_AndFindsOneFiguresSprites()
    {
        var r = new byte[SpriteBand.Size];
        r[0] = 0x78;
        byte[] xy = [10, 0, 10, 21, 60, 0, 60, 21];
        xy.CopyTo(r, 1);
        r[17] = 5;
        r[18] = 0x1D; // colour nibble only
        r[25] = 0x80;
        byte[] blocks = [1, 0, 1, 0];
        blocks.CopyTo(r, 26);
        (r[34], r[35], r[36], r[37]) = (0x00, 0x00, 0x0F, 0x8C);
        var band = SpriteBand.Parse(r);
        Assert.Equal((0x78, 0x80, 0x0F, 0x8C), (band.BaseY, band.PointerBase, band.Enable, band.NextRaster));
        Assert.Equal((10, 21), band.Positions[1]);
        Assert.Equal(13, band.Colours[1]);
        Assert.Equal(2, band.PartsPerFigure); // blocks 1, 0, then block 1 again: the second figure

        var data = new byte[2 * 64];
        data[0] = 0x80;       // block 0 (0, 0): the lower sprite
        data[64 + 62] = 0x01; // block 1 (23, 20): the upper sprite
        var figure = band.Figure(data);
        Assert.Equal((24, 42), (figure.Width, figure.Height));
        Assert.Equal(Vic(5), Px(figure, 23, 20));
        Assert.Equal(Vic(13), Px(figure, 0, 21));
    }

    [RequiresFilesFact(Side1, Side2, ArtRoot)]
    public void Art_EqualsTheCommittedExtract()
    {
        var files = Art.Value.Files;
        var dir = Repo.Path(ArtRoot);
        var n = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            Assert.True(files.TryGetValue(rel, out var bytes), $"{rel}: committed but not produced");
            Golden.PngEqualsFile(bytes!, $"{ArtRoot}/{rel}");
            n++;
        }
        Assert.Equal(files.Count, n);
        // monsters idle + attacking (s219: every monster), portraits (2 x 15), title, town, 13 tile classes x 2, 4 map sprites, 9 dungeon
        // glyphs, 17 item pictures (s221: WEAPON, GOOD WEAPON, MG WEAPON twice, by colour), 3 character-set sheets; s221: the
        // ending (Zeus and 3 states, ZALL's 17 sprite blocks, its font) and the party wipe (3 temples, 3 sprites, the font)
        Assert.Equal(80 + 80 + 30 + 1 + 1 + 26 + 4 + 9 + 17 + 3 + (4 + 17 + 1) + (3 + 3 + 1), n);
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Pictures_MatchTheirPixelChecksums_AndTheLiveScreens()
    {
        var files = Art.Value.Files;
        string Sum(string rel) => Convert.ToHexString(SHA256.HashData(Png.Decode(files[rel]).Pixels))[..16];
        // Fixed checksums of the RGBA pixels (s218): a change in a decoder shows here even when the extract is regenerated.
        Assert.Equal("09F727BD58F384C9", Sum("screens/title.png"));
        Assert.Equal("230B9D59A4EC4EED", Sum("monsters/idle/01-bantir.png"));
        Assert.Equal("35C083DAA5BE7B7A", Sum("monsters/idle/76-black-knight.png"));
        Assert.Equal("970C15CEB1EC8544", Sum("portraits/idle/00-human.png"));
        Assert.Equal("60ED3B6BB9AF7663", Sum("screens/town.png"));

        // As the live screens show them (s218, VICE): BANTIR one green sprite; BLACK KNIGHT four expanded sprites.
        var bantir = Png.Decode(files["monsters/idle/01-bantir.png"]);
        Assert.Equal((24, 21), (bantir.Width, bantir.Height));
        var knight = Png.Decode(files["monsters/idle/76-black-knight.png"]);
        Assert.Equal((96, 126), (knight.Width, knight.Height));
        Assert.Equal((144, 126), Art.Value.LargestMonster); // PYTHON 144 wide, BLACK KNIGHT 126 tall
        // The town's guild in yellow at text column 1, row 9 (its first character's top left pixel row).
        var town = Png.Decode(files["screens/town.png"]);
        Assert.Contains(Enumerable.Range(8, 48).SelectMany(x => Enumerable.Range(72, 48).Select(y => Px(town, x, y))), p => p == Vic(7));
        // Water (class 10) in blue, fog black.
        var water = Png.Decode(files["overworld/tiles/class-10-even.png"]);
        Assert.Contains(Enumerable.Range(0, 64).Select(i => Px(water, i % 8, i / 8)), p => p == Vic(6));
        var fog = Png.Decode(files["overworld/tiles/fog-odd.png"]);
        Assert.All(Enumerable.Range(0, 64).Select(i => Px(fog, i % 8, i / 8)), p => Assert.Equal(Vic(0), p));
    }

    /// <summary>
    /// s221: the ending and party-wipe pictures as P.END and CEMETARY show them (GRAPHICS.md 9; the live dumps matched
    /// extract_c64_end_screens.py pixel for pixel, which the committed extract holds): Zeus white in his 12 x 13 cells, the
    /// strike adding only the light blue hand, the cloud in its two multicolour colours, the temple dark grey in its 18 x 10
    /// cells, the face only in the doorway, the door only behind the characters.
    /// </summary>
    [RequiresFilesFact(Side1, Side2)]
    public void EndScreens_AsPEndAndCemeteryShowThem()
    {
        var files = Art.Value.Files;
        RgbaImage P(string rel) => Png.Decode(files[rel]);
        string Sum(string rel) => Convert.ToHexString(SHA256.HashData(P(rel).Pixels))[..16];
        Assert.Equal("81A246A91BC6258B", Sum("ending/zeus.png"));
        Assert.Equal("14D051C6DE9DDC8F", Sum("defeat/temple-demon.png"));
        IEnumerable<(int X, int Y)> All(RgbaImage img) =>
            Enumerable.Range(0, img.Height).SelectMany(y => Enumerable.Range(0, img.Width).Select(x => (x, y)));

        var zeus = P("ending/zeus.png");
        Assert.Equal((320, 200), (zeus.Width, zeus.Height));
        foreach (var (x, y) in All(zeus))
        {
            var inCells = x is >= 104 and < 200 && y is >= 16 and < 120; // text columns 13-24, rows 2-14
            Assert.True(Px(zeus, x, y) == Vic(0) || inCells && Px(zeus, x, y) == Vic(1), $"zeus ({x}, {y})");
        }
        var strike = P("ending/zeus-strike.png");
        var changed = All(zeus).Where(p => Px(zeus, p.X, p.Y) != Px(strike, p.X, p.Y)).ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, p => Assert.True(p.X is >= 178 and < 202 && p.Y is >= 12 and < 33 && Px(strike, p.X, p.Y) == Vic(14)));
        var cloud = P("ending/sprites/d0-cloud.png");
        Assert.Equal((24, 21), (cloud.Width, cloud.Height));
        var colours = All(cloud).Select(p => Px(cloud, p.X, p.Y)).Where(c => c.Item4 != 0).Distinct().ToHashSet();
        Assert.Equal(new HashSet<(byte, byte, byte, byte)> { Vic(2), Vic(7) }, colours);

        var temple = P("defeat/temple.png");
        foreach (var (x, y) in All(temple))
        {
            var inCells = x is >= 80 and < 224 && y < 80; // text columns 10-27, rows 0-9
            Assert.True(Px(temple, x, y) == Vic(0) || inCells && Px(temple, x, y) == Vic(11), $"temple ({x}, {y})");
        }
        var demon = P("defeat/temple-demon.png");
        Assert.All(All(temple).Where(p => Px(temple, p.X, p.Y) != Px(demon, p.X, p.Y)),
            p => Assert.True(p.X is >= 124 and < 172 && p.Y is >= 31 and < 73 && Px(demon, p.X, p.Y) == Vic(4)));
        var door = P("defeat/temple-door.png");
        Assert.All(All(temple).Where(p => Px(temple, p.X, p.Y) != Px(door, p.X, p.Y)),
            p => Assert.True(Px(temple, p.X, p.Y) == Vic(0) && Px(door, p.X, p.Y) == Vic(15))); // behind the characters
        Assert.Equal((24, 21), (P("defeat/ghost.png").Width, P("defeat/ghost.png").Height));
        Assert.Equal((48, 42), (P("defeat/demon-face.png").Width, P("defeat/demon-face.png").Height));
        Assert.DoesNotContain("defeat/temple-demon-doorway.png", files.Keys); // II's pack only
    }

    private const string EnA = "reverse-engineering/c64/original-images/phantasie1/en/Phantasie_I_1986_SSI_Side_A.d64";
    private const string EnB = "reverse-engineering/c64/original-images/phantasie1/en/Phantasie_I_1986_SSI_Side_B.d64";

    /// <summary>The held en pair (a played v1.4 the profile also takes) gives the same art.</summary>
    [RequiresFilesFact(Side1, Side2, EnA, EnB)]
    public void TheHeldEnPair_GivesTheSameArt()
    {
        var vols = new[] { EnA, EnB }.Select(p => D64Volume.TryOpen(File.ReadAllBytes(Repo.Path(p)))!).ToList();
        var en = new P1C64Art(null);
        en.Run(name => vols.Select(v => v.Files.FirstOrDefault(e => e.Name == name) is { } e ? v.Read(e) : null)
            .FirstOrDefault(d => d is not null));
        Assert.Equal(Art.Value.Files.Keys.Order(StringComparer.Ordinal), en.Files.Keys.Order(StringComparer.Ordinal));
        foreach (var (rel, bytes) in Art.Value.Files)
            Assert.True(Png.Decode(bytes).PixelEquals(Png.Decode(en.Files[rel])), $"{rel}: the en pair's differs");
    }

    /// <summary>
    /// s221: each item / feature picture in the one colour the programs print it in (GRAPHICS.md 7, read in OUTDOORS /
    /// DUNGEON's bytecode): the find colours by id, TRAP 10, STAIRS 12, LOCK 9, EXIT 5; DOOR white (never shown, [?]).
    /// </summary>
    [RequiresFilesFact(Side1, Side2)]
    public void ItemPictures_AreInTheProgramsColours()
    {
        var files = Art.Value.Files;
        var expected = new Dictionary<string, (int Colour, int W, int H)>
        {
            ["shield"] = (15, 56, 56), ["armor"] = (15, 56, 56), ["weapon"] = (15, 56, 56), ["weapon-yellow"] = (7, 56, 56),
            ["good-weapon"] = (15, 56, 56), ["good-weapon-light-blue"] = (14, 56, 56), ["mg-weapon"] = (15, 56, 56),
            ["mg-weapon-cyan"] = (3, 56, 56), ["potion"] = (15, 56, 56), ["scroll"] = (7, 56, 56), ["treasure"] = (7, 56, 56),
            ["good-treasure"] = (7, 56, 56), ["door"] = (1, 56, 56), ["trap"] = (10, 56, 56), ["stairs"] = (12, 56, 56),
            ["lock"] = (9, 56, 56), ["exit"] = (5, 64, 48),
        };
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal),
            files.Keys.Where(k => k.StartsWith("items/", StringComparison.Ordinal)).Select(k => k[6..^4]).Order(StringComparer.Ordinal));
        foreach (var (name, (colour, w, h)) in expected)
        {
            var img = Png.Decode(files[$"items/{name}.png"]);
            Assert.Equal((w, h), (img.Width, img.Height));
            var inked = Enumerable.Range(0, w * h).Select(i => Px(img, i % w, i / w)).Where(p => p.Item4 != 0).Distinct().ToList();
            Assert.Equal([Vic(colour)], inked);
        }
        // The two colourings of one file have the same pixels.
        var plain = Png.Decode(files["items/weapon.png"]);
        var yellow = Png.Decode(files["items/weapon-yellow.png"]);
        Assert.All(Enumerable.Range(0, 56 * 56), i => Assert.Equal(Px(plain, i % 56, i / 56).Item4, Px(yellow, i % 56, i / 56).Item4));
    }

    /// <summary>s221: the pack's event.* slots and find_pictures (item id ranges, the C64's (IT - 1) / 20 with its float colour tests).</summary>
    [Fact]
    public void Pack_HasTheEventSlots_AndTheFindTable()
    {
        var pack = P1C64ArtManifest.Pack();
        var pictures = pack["pictures"]!.AsObject();
        Assert.Equal("items/trap.png", (string?)pictures["event.trap"]);
        Assert.Equal("items/stairs.png", (string?)pictures["event.stairs"]);
        Assert.Equal("items/lock.png", (string?)pictures["event.lock"]);
        Assert.Equal("items/exit.png", (string?)pictures["event.exit"]);
        // DOOR's item picture is never shown (s223: defeat/door.png is CEMETARY's door sprite, another picture)
        Assert.DoesNotContain(pictures, p => ((string?)p.Value)!.EndsWith("items/door.png", StringComparison.Ordinal));
        var rows = pack["find_pictures"]!.AsArray().Select(r => ((int)r![0]!, (int)r[1]!, (string)r[2]!)).ToList();
        // Every id 1..180 in exactly one row, in order; each row's picture is a slot of the pack.
        Assert.Equal(Enumerable.Range(1, 180), rows.SelectMany(r => Enumerable.Range(r.Item1, r.Item2 - r.Item1 + 1)));
        Assert.All(rows, r => Assert.True(pictures.ContainsKey(r.Item3), r.Item3));
        string For(int id) => rows.Single(r => id >= r.Item1 && id <= r.Item2).Item3;
        Assert.Equal("event.find.weapon-yellow", For(41));
        Assert.Equal("event.find.weapon", For(45));
        Assert.Equal("event.find.good-weapon-light-blue", For(61));
        Assert.Equal("event.find.mg-weapon-cyan", For(81));
        Assert.Equal("event.find.mg-weapon", For(100));
        Assert.Equal("event.find.potion", For(101));
        Assert.Equal("event.find.good-treasure", For(180));
        // II's pack: the same slots and table (its programs run the same bytecode on its ids).
        var ii = OpenPhantasie.Importer.Pipelines.P2C64.P2C64ArtManifest.Pack();
        Assert.Equal(pack["find_pictures"]!.ToJsonString(), ii["find_pictures"]!.ToJsonString());
        Assert.Equal("items/exit.png", (string?)ii["pictures"]!["event.exit"]);
    }

    [RequiresFilesFact(Shipped)]
    public void Pack_IsByteEqualToTheShippedC64Json()
    {
        var bytes = PyJson.SerializeUtf8(P1C64ArtManifest.Pack(), 2, ensureAscii: true);
        Assert.True(Golden.BytesEqualFile(bytes, Shipped), "c64.json differs from the import's");
    }

    [RequiresFilesFact(Side1, Side2)]
    public void Import_WritesTheArtAndThePack()
    {
        var run = PipelineHarness.Run("p1-c64", Side1, Side2);
        Assert.Contains("GameData/phantasie1/graphics/c64.json", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/monsters/idle/00-skeleton.png", run.AllOutputs.Keys);
        Assert.Contains($"files/{ArtRoot}/overworld/party.png", run.AllOutputs.Keys);
    }
}
