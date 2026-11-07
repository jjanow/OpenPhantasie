using System.Buffers.Binary;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s98 (user: never crop scenes): the town screen shows each pack's whole town picture 1:1 on the whole screen, the
/// remaster's bar over the picture's own bar rows and the name where the original puts it (pack <c>town_screen</c>).
/// s102: a message goes on the picture's own blank rows where it has some (<see cref="TownScreenLayout.MessageY"/>).
/// </summary>
public class TownScreenLayoutTests
{
    public static TheoryData<GameId, string, int, int, int, string, int?> Packs => new()
    {
        // game, pack, bar rows, name centre x, name y, colour, message y
        // pelnor.pic: menu rows 0..10, art 11..177, black 178..199; name and messages share line 185 (pen 2, 0x818) of
        // the town palette 0x1947C (0xE24, s120 capture)
        { GameId.P1, "amiga", 11, 160, 185, "#EE2244", 184 },
        { GameId.P1, "dos-cga", 9, 160, 191, "#FFFFFF", 191 }, // DOS: rows 0..8 and 191..199 blank for the bar and name
        { GameId.P2, "atari-st", 10, 156, 24, "#000000", 192 }, // PELNOR.PIC: GEM bar 0..9, name scroll, message y 192 (0x4082)
        // w4: black rows 0..11, the empty scroll at the top (name pen 2, 0x7e54), art to 199
        { GameId.P3, "amiga", 12, 160, 16, "#924924", null },
    };

    private static GraphicsPack? Pack(GameId game, string id) =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game)).Find(id);

    [Theory]
    [MemberData(nameof(Packs))]
    public void TownPicture_IsAWholeScreen_AndThePackPlacesBarAndName(GameId game, string id, int bar, int nameX, int nameY,
        string colour, int? messageY)
    {
        var pack = Pack(game, id);
        if (pack is null)
            return; // extracts not present on this machine
        Assert.Equal(new TownScreenLayout(bar, nameX, nameY, colour, messageY), pack.TownScreen with
        {
            Doors = null, DoorSeconds = 0, NameCells = null, NameField = null, DimTowns = TownScreenLayout.Default.DimTowns, DimFrom = null,
            DimPalette = null, DimNameColour = null, LeavingColour = null, LeavingSeconds = 0, LeavingBlackSeconds = 0, // III's cell centring and dim towns: Phantasie3GraphicsPackTests; s154 the leaving line: P1ReportWindowTests
            NameColours = TownScreenLayout.Default.NameColours, FreshNameX = null, // II's per-town ink: TownName_StColourPerTown
            Font = null, FontFile = null, // I Amiga's z6font: P1AmigaFontTests.TownName_IsZ6Font_AsOnTheAmigaShot
        });
        var file = pack.Resolve(PictureNames.TownBackdrop);
        Assert.NotNull(file);
        var header = File.ReadAllBytes(file!).AsSpan(16, 8); // PNG IHDR width, height
        Assert.Equal((320, 200), (BinaryPrimitives.ReadInt32BigEndian(header), BinaryPrimitives.ReadInt32BigEndian(header[4..])));
    }

    /// <summary>
    /// s120 (FS-UAE capture of Pelnor, <c>parity/shots/p1-amiga-town-name.png</c>): the Amiga I centres the town record's
    /// 10-character space-padded name field (<c>h80:0x818</c>), so PELNOR's ink starts at x 130, not at the trimmed
    /// name's centred 142; a pack without a name field centres the name as it is.
    /// </summary>
    [Fact]
    public void TownName_AmigaI_CentresThePaddedField()
    {
        var amiga = Pack(GameId.P1, "amiga");
        if (amiga is null)
            return;
        Assert.Equal(10, amiga.TownScreen.NameField);
        Assert.Equal("PELNOR    ", amiga.TownScreen.FieldName("PELNOR"));
        Assert.Equal(130, UiOverlays.TownNameLeft(amiga.TownScreen, "PELNOR", freshStart: false));
        Assert.Equal(130, UiOverlays.TownNameLeft(amiga.TownScreen, "PHANTASIA", freshStart: false)); // every name: 130
        Assert.Equal(160 - PixelFont.Measure("PELNOR") / 2, UiOverlays.TownNameLeft(TownScreenLayout.Default, "PELNOR", false));
        Assert.Null(Pack(GameId.P1, "dos-cga")?.TownScreen.NameField);
        Assert.Null(Pack(GameId.P3, "amiga")?.TownScreen.NameField);
    }

    /// <summary>
    /// s102: III's open doors (Amiga <c>0xacc8</c>, table <c>0x23070</c>): five 48×48 squares of <c>D/w3</c>, each at its
    /// building on the street, flashed for 15 ticks on going in. The other packs have none.
    /// </summary>
    [Fact]
    public void TownDoors_OnlyIII_AtTheirBuildings()
    {
        var p3 = Pack(GameId.P3, "amiga");
        if (p3 is null)
            return;
        var doors = p3.TownScreen.Doors!;
        Assert.Equal(new (int, int)[] { (16, 141), (64, 141), (160, 143), (208, 139), (272, 141) },
            new[] { "bank", "mystic", "armory", "inn", "guild" }.Select(d => doors[d]));
        Assert.Equal(48, p3.TownScreen.DoorSize);
        Assert.Equal(0.3, p3.TownScreen.DoorSeconds, 3);
        Assert.NotNull(p3.Resolve(PictureNames.TownDoors));
        Assert.Null(Pack(GameId.P1, "amiga")?.TownScreen.Doors);
        Assert.Null(Pack(GameId.P2, "atari-st")?.TownScreen.Doors);
    }

    [Theory]
    [MemberData(nameof(Packs))]
    public void MessageBand_StaysOnScreen_AndOffTheName(GameId game, string id, int bar, int nameX, int nameY, string colour,
        int? messageY)
    {
        _ = (game, id);
        var layout = new TownScreenLayout(bar, nameX, nameY, colour, messageY);
        var top = UiOverlays.TownBandTop(layout);
        var height = UiOverlays.TownBandHeight(layout);
        Assert.InRange(top, bar, UiOverlays.ScreenBottom - PixelFont.GlyphHeight - 1); // room for the text one row down
        Assert.InRange(top + height, top + PixelFont.GlyphHeight + 1, UiOverlays.ScreenBottom);
        var nameBottom = nameY + PixelFont.GlyphHeight;
        // DOS, Amiga I: the message takes the name's own line while it shows (the band covers the whole name).
        var replacesName = top <= nameY && top + height >= nameBottom;
        Assert.True(replacesName || top + height <= nameY || top >= nameBottom, $"band {top} overlaps the name at {nameY}");
    }

    /// <summary>s102: the band's one line lies on rows the picture leaves blank (one colour), so it covers no art.</summary>
    [Theory]
    [MemberData(nameof(Packs))]
    public void MessageBand_OnTheBlankRows_CoversNoArt(GameId game, string id, int bar, int nameX, int nameY, string colour,
        int? messageY)
    {
        if (messageY is null || Pack(game, id)?.Resolve(PictureNames.TownBackdrop) is not { } file)
            return;
        var layout = new TownScreenLayout(bar, nameX, nameY, colour, messageY);
        var top = UiOverlays.TownBandTop(layout);
        var rows = BlankRows(file);
        for (var y = top; y < top + UiOverlays.TownBandHeight(layout); y++)
            Assert.True(rows.Contains(y), $"{game} {id}: band row {y} is part of the picture");
    }

    /// <summary>
    /// s109 live (Hatari, <c>atari-st/phantasie2/parity/shots/p2-town-*.png</c>): the ST banner's ink is the town number
    /// as a VDI pen (town 3 → pen 11, 9 → pen 13) in the <c>town</c> palette; a new game's first Pippacott is black
    /// (town global 0). The other packs keep one colour.
    /// </summary>
    [Fact]
    public void TownName_StColourPerTown()
    {
        var st = Pack(GameId.P2, "atari-st");
        if (st is null)
            return;
        var t = st.TownScreen;
        Assert.Equal(["#FFFFFF", "#009100", "#006DB6", "#006D00", "#6D2400", "#6D6D24", "#910000", "#484848", "#482400", "#6D6D6D"],
            Enumerable.Range(1, 10).Select(n => t.NameColourFor(n)));
        Assert.Equal("#000000", t.NameColourFor(1, freshStart: true));
        Assert.Equal("#000000", t.NameColourFor(null));
        Assert.Equal((156, 151), (t.NameXFor(freshStart: false), t.NameXFor(freshStart: true))); // the nudge reads town 0 too
        var amiga = Pack(GameId.P1, "amiga");
        if (amiga is not null)
            Assert.All(Enumerable.Range(0, 13), n => Assert.Equal("#EE2244", amiga.TownScreen.NameColourFor(n, n == 0)));
        Assert.Equal("#FFFFFF", TownScreenLayout.Default.NameColourFor(5));
        Assert.Equal(160, TownScreenLayout.Default.NameXFor(freshStart: true));
    }

    [Fact]
    public void PackWithoutTownScreen_GetsTheDosLayout() =>
        Assert.Equal(new TownScreenLayout(9, 160, 191, "#FFFFFF", 191), TownScreenLayout.Default);

    [Fact]
    public void MessageBandClock_ShowsAChange_AndARestartedRepeat_ButNotAPassThrough()
    {
        var clock = new MessageBandClock();
        Assert.True(clock.Shows("NOT ENOUGH GOLD", 10.0, 4.0));
        Assert.True(clock.Shows("NOT ENOUGH GOLD", 13.0, 4.0)); // the same text passed through: the same clock
        Assert.False(clock.Shows("NOT ENOUGH GOLD", 14.5, 4.0)); // expired
        clock.Restart(); // the action ran again and gave the same text
        Assert.True(clock.Shows("NOT ENOUGH GOLD", 20.0, 4.0));
        Assert.False(clock.Shows("NOT ENOUGH GOLD", 24.0, 4.0));
        Assert.True(clock.Shows("LEFT THE GUILD.", 24.0, 4.0)); // a new text
    }

    /// <summary>Rows of one colour in an 8-bit RGB / RGBA, non-interlaced PNG (the town pictures' format).</summary>
    private static HashSet<int> BlankRows(string png)
    {
        var bytes = File.ReadAllBytes(png);
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20));
        Assert.Equal(8, bytes[24]);
        var bpp = bytes[25] switch { 2 => 3, 6 => 4, var t => throw new NotSupportedException($"PNG colour type {t}") };
        Assert.Equal(0, bytes[28]);
        using var idat = new MemoryStream();
        for (var at = 8; at < bytes.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at));
            if (System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4) == "IDAT")
                idat.Write(bytes, at + 8, length);
            at += 12 + length;
        }
        idat.Position = 0;
        using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        z.CopyTo(raw);
        var data = raw.ToArray();
        var stride = width * bpp;
        var prev = new byte[stride];
        var blank = new HashSet<int>();
        for (var y = 0; y < height; y++)
        {
            var filter = data[y * (stride + 1)];
            var row = data.AsSpan(y * (stride + 1) + 1, stride).ToArray();
            for (var i = 0; i < stride; i++)
            {
                int a = i >= bpp ? row[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                row[i] += filter switch
                {
                    1 => (byte)a,
                    2 => (byte)b,
                    3 => (byte)((a + b) / 2),
                    4 => (byte)Paeth(a, b, c),
                    _ => 0,
                };
            }
            var same = true;
            for (var x = bpp; x < stride && same; x += bpp)
                same = row[x] == row[0] && row[x + 1] == row[1] && row[x + 2] == row[2];
            if (same)
                blank.Add(y);
            prev = row;
        }
        return blank;

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
