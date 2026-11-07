using System.Buffers.Binary;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The title splash (s102): each game's start-up pictures in the order and for the time its original showed them (the
/// pack's <c>title_sequence</c>; boot sequences in the platforms' FORMAT.md "Title" notes), drawn whole on the
/// sequence's canvas, with a per-game Options switch (on by default).
/// </summary>
public class TitleSplashTests
{
    private static GraphicsPack DefaultPack(GameId game) =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game)).Default
        ?? throw new InvalidOperationException($"{game} extracts missing");

    private static TitleSequence Sequence(GameId game) =>
        DefaultPack(game).TitleSequence ?? throw new InvalidOperationException($"{game} has no title_sequence");

    internal static (int W, int H) PngSize(string path)
    {
        var head = File.ReadAllBytes(path).AsSpan(16, 8);
        return (BinaryPrimitives.ReadInt32BigEndian(head), BinaryPrimitives.ReadInt32BigEndian(head[4..]));
    }

    [OriginalDataFact]
    public void PhantasieI_Amiga_Cover1ForTheTune_ThenTheCreditsForDelay250()
    {
        // h77 +0x2de: cover1 while tamb.dat plays (2678 dos ticks = 53.56 s, s108; a key ends it); main's Delay(250) =
        // 5 s on cover2.
        var s = Sequence(GameId.P1);
        Assert.Equal((320, 200), (s.Width, s.Height));
        Assert.Equal([PictureNames.TitleCover, PictureNames.TitleCredits], s.Steps.Select(t => t.Picture));
        Assert.Equal([53.56, 5.0], s.Steps.Select(t => Math.Round(t.Seconds!.Value, 6)));
    }

    [OriginalDataFact]
    public void PhantasieI_DosCga_HasNoTitle_SoNothingCgaIsShown()
    {
        var cga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("dos-cga")!;
        Assert.Null(cga.TitleSequence);
        Assert.Null(cga.Resolve(PictureNames.TitleCredits));
    }

    [OriginalDataFact]
    public void PhantasieII_St_Cover1For251Vbls_ThenTheCompositeUntilTheTuneEnds()
    {
        // START.PRG: dbf from 250 = 251 Vsyncs at the pack's colour-cycle rate (60 Hz [~]), then COVER2 over COVER1.
        // s156: PHANT.PRG then waits at 0xb8f2 for the Dosound script (64.62 s) before its first screen.
        // s158: the tune starts 1.96 s before COVER1 shows (COVER1 read on the black palette): a blank first step.
        var pack = DefaultPack(GameId.P2);
        var s = pack.TitleSequence!;
        Assert.Equal([null, PictureNames.TitleCover, PictureNames.TitleCredits], s.Steps.Select(t => t.Picture));
        Assert.Empty(s.Steps[0].Texts);
        Assert.Equal(1.96, s.Steps[0].Seconds!.Value, 9);
        Assert.Equal(251.0 / pack.ColourCycle!.VblHz, s.Steps[1].Seconds!.Value, 9);
        Assert.Null(s.Steps[1].EndsAt);
        Assert.Equal(64.62, s.Steps[2].EndsAt!.Value, 6);
        Assert.Equal(64.62 - 1.96 - 251.0 / 60, s.Steps[2].Seconds!.Value, 6);
    }

    [Fact]
    public void Splash_AStepUntilTheTuneEnds_EndsAtTheTunesEnd_EvenWhenAKeyCutTheStepBefore()
    {
        var seq = new TitleSequence(320, 200, "#000000",
        [
            new TitleStep("title.cover", 0, 0, [], 4.0),
            new TitleStep("title.credits", 0, 0, [], 60.0) { EndsAt = 64.0 },
        ]);
        var held = new TitleSplash(GameId.P2, seq);
        Assert.False(held.Update(4.0, advance: false, skipAll: false));
        Assert.Equal(1, held.Step);
        Assert.False(held.Update(59.9, advance: false, skipAll: false));
        Assert.True(held.Update(0.2, advance: false, skipAll: false));
        var cut = new TitleSplash(GameId.P2, seq);
        Assert.False(cut.Update(1.0, advance: true, skipAll: false)); // a key ends COVER1 after 1 s
        Assert.False(cut.Update(62.9, advance: false, skipAll: false));
        Assert.True(cut.Update(0.2, advance: false, skipAll: false)); // 64 s from the start, not 1 + 60
    }

    [OriginalDataFact]
    public void PhantasieIII_Amiga_TheTitleUntilAKey_AsTheManualSays()
    {
        var s = Sequence(GameId.P3);
        var step = Assert.Single(s.Steps);
        Assert.Equal(PictureNames.TitleCover, step.Picture);
        Assert.Null(step.Seconds); // manual: "Hit any key to get to the Initial Menu" (D3-065)
    }

    [OriginalDataFact]
    public void PhantasieIV_X68000_StarcraftPresents_ThenThePictureWithItsCreditLine()
    {
        // _openning: window "Phantasie IV" (320×216), "STARCRAFT Presents" at window y 84, OPEN.PKH at (0, 0), the
        // credit at window y 200 under it. The canvas holds _wopen's 21-row title bar (seen in XEiJ, s110) and the whole
        // window; the torches play the pack's "title" animation over the picture.
        var pack = DefaultPack(GameId.P4);
        Assert.Equal("x68000", pack.Id);
        var s = pack.TitleSequence!;
        // s151: the canvas is screen pixels (216, 73)-(722, 420): window 32's bar and area, and window 34 (_disk(0)'s panel)
        // at _cvx(496) = 585, _cvy(48) = 94 under its own 21-row bar, 136 x 324 with frame and shadow.
        Assert.Equal((585 + 136 + 1 - 216, 94 + 324 + 2 - 73), (s.Width, s.Height));
        Assert.Equal((0, 112 - 73), (s.Steps[1].X, s.Steps[1].Y));
        Assert.Equal([null, "disk"], s.Steps.Select(t => t.Menu));
        Assert.Equal((585 - 216, 0), (s.Steps[1].MenuX, s.Steps[1].MenuY));
        Assert.Equal([null, "title"], s.Steps.Select(t => t.Animation));
        Assert.Equal([null, PictureNames.TitleCover], s.Steps.Select(t => t.Picture));
        Assert.All(s.Steps, t => Assert.Null(t.Seconds));
        var p4 = UiText.For(GameId.P4);
        Assert.Equal(["Phantasie IV", "STARCRAFT Presents"], s.Steps[0].Texts.Select(t => p4.Get(t.StringId)));
        Assert.Equal(["Phantasie IV", "By Winston Douglas Wood"], s.Steps[1].Texts.Select(t => p4.Get(t.StringId)));
        Assert.Equal((320, 200), PngSize(pack.Resolve(PictureNames.TitleCover)!));
        Assert.True(s.Steps[1].Texts[1].Y >= s.Steps[1].Y + 200); // the credit line is under the picture, not on it
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    [InlineData(GameId.P4)]
    public void EveryStep_DrawsWhole_OnItsCanvas(GameId game)
    {
        // Nothing cropped or shrunk: each picture at its own size inside the canvas; each text line inside it too.
        var pack = DefaultPack(game);
        var s = pack.TitleSequence!;
        var table = UiText.For(game);
        foreach (var step in s.Steps)
        {
            if (step.Picture is { } name)
            {
                var path = pack.Resolve(name);
                Assert.NotNull(path);
                var (w, h) = PngSize(path!);
                Assert.InRange(step.X, 0, s.Width - w);
                Assert.InRange(step.Y, 0, s.Height - h);
            }
            foreach (var line in step.Texts)
            {
                Assert.True(table.Contains(line.StringId), line.StringId);
                var width = PixelFont.Measure(table.Get(line.StringId));
                Assert.InRange(line.CentreX - width / 2, 0, s.Width - width);
                Assert.InRange(line.Y, 0, s.Height - PixelFont.LineHeight + 1);
            }
        }
    }

    [OriginalDataFact]
    public void AmigaCover_IsDecodedInItsTitlePalette()
    {
        // LoadRGB4 of h69 +0xb4 (0x194DC) after the blit: register 2 = 0xEB0 (the yellow of the SSI / Andromeda logos)
        // shows c * 17 per gun; the old combat-palette decode had no such colour.
        using var image = File.OpenRead(DefaultPack(GameId.P1).Resolve(PictureNames.TitleCover)!);
        var tex = PngColours(image);
        Assert.Contains((0xEE, 0xBB, 0x00), tex);
        Assert.Contains((0x88, 0x22, 0x00), tex); // register 1 = 0x820, the door
    }

    /// <summary>The set of colours of an 8-bit RGB(A) or palette PNG (decoded with System.Drawing-free zlib).</summary>
    internal static HashSet<(int, int, int)> PngColours(Stream png)
    {
        using var ms = new MemoryStream();
        png.CopyTo(ms);
        var data = ms.ToArray();
        int p = 8, width = 0, height = 0, colourType = 0, depth = 0;
        var idat = new MemoryStream();
        List<(int, int, int)> palette = [];
        while (p < data.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(p));
            var type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
            var body = data.AsSpan(p + 8, len);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(body);
                height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                depth = body[8];
                colourType = body[9];
            }
            else if (type == "PLTE")
                for (var i = 0; i + 2 < len; i += 3)
                    palette.Add((body[i], body[i + 1], body[i + 2]));
            else if (type == "IDAT")
                idat.Write(body);
            p += 12 + len;
        }
        Assert.Equal(8, depth);
        var bpp = colourType switch { 2 => 3, 6 => 4, 3 => 1, _ => throw new NotSupportedException($"PNG colour type {colourType}") };
        idat.Position = 0;
        using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        var raw = new MemoryStream();
        z.CopyTo(raw);
        var bytes = raw.ToArray();
        var stride = width * bpp;
        var prev = new byte[stride];
        var cur = new byte[stride];
        var colours = new HashSet<(int, int, int)>();
        for (var y = 0; y < height; y++)
        {
            var filter = bytes[y * (stride + 1)];
            Array.Copy(bytes, y * (stride + 1) + 1, cur, 0, stride);
            for (var i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                cur[i] = (byte)(cur[i] + filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                });
            }
            for (var x = 0; x < width; x++)
                colours.Add(colourType == 3 ? palette[cur[x]] : (cur[x * bpp], cur[x * bpp + 1], cur[x * bpp + 2]));
            (prev, cur) = (cur, prev);
        }
        return colours;

        static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }

    private static TitleSequence Timed() => new(320, 200, "#000000",
    [
        new TitleStep("title.cover", 0, 0, [], null),
        new TitleStep("title.credits", 0, 0, [], 5.0),
    ]);

    [Fact]
    public void Splash_WaitsForAKey_ThenHoldsATimedStep_ThenEnds()
    {
        var splash = new TitleSplash(GameId.P1, Timed());
        Assert.False(splash.Update(60, advance: false, skipAll: false)); // the first step waits for a key, however long
        Assert.Equal(0, splash.Step);
        Assert.False(splash.Update(0.016, advance: true, skipAll: false));
        Assert.Equal(1, splash.Step);
        Assert.False(splash.Update(4.9, advance: false, skipAll: false));
        Assert.True(splash.Update(0.2, advance: false, skipAll: false)); // 5 s on the credits
        Assert.True(splash.Done);
        Assert.Null(splash.Current);
    }

    [Fact]
    public void Splash_AKeyEndsATimedStepEarly_AndEscEndsItAll()
    {
        var splash = new TitleSplash(GameId.P1, Timed());
        splash.Update(0, advance: true, skipAll: false);
        Assert.True(splash.Update(0.1, advance: true, skipAll: false)); // a key ends the credits too
        var esc = new TitleSplash(GameId.P1, Timed());
        Assert.True(esc.Update(0, advance: false, skipAll: true));
        Assert.Equal(2, esc.Step);
    }

    [Fact]
    public void TitleScreenSwitch_IsOnByDefault_AndSaved()
    {
        Assert.True(new GameSettings().TitleScreen);
        var settings = new ClientSettings();
        settings.Game(GameId.P2).TitleScreen = false;
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        Assert.Contains("\"TitleScreen\":false", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(new[] { "--title-screen", "4" }, GameId.P4, false)]
    [InlineData(new[] { "--title-screen", "1" }, GameId.P1, false)]
    [InlineData(new[] { "--title-screen", "5" }, null, true)]
    [InlineData(new[] { "--title-screen" }, null, true)]
    [InlineData(new string[0], null, false)]
    public void TitlePreviewSwitch_TakesAGame(string[] args, GameId? game, bool error)
    {
        Assert.Equal(game, TitlePreview.ParseArgs(args, out var message));
        Assert.Equal(error, message is not null);
    }

    [Theory]
    [InlineData("""{ "steps": [ { "picture": "title.cover", "seconds": 1, "vbl": 50 } ] }""", "both seconds and vbl")]
    [InlineData("""{ "steps": [ { "picture": "title.cover", "vbl": 50 } ] }""", "vbl without a vbl_hz")]
    [InlineData("""{ "steps": [ { } ] }""", "an empty step")]
    [InlineData("""{ "steps": [ { "seconds": 1 } ] }""", "an empty step")]
    [InlineData("""{ "steps": [ { "blank": true } ] }""", "a blank step")]
    [InlineData("""{ "steps": [ { "blank": true, "seconds": 1, "picture": "title.cover" } ] }""", "a blank step")]
    [InlineData("""{ "steps": [] }""", "no steps")]
    [InlineData("""{ "canvas": [0, 200], "steps": [ { "picture": "title.cover" } ] }""", "bad canvas")]
    [InlineData("""{ "tune": { "format": "amiga-p3-music", "file": "m0", "until_step": 1, "plays_on": true }, "steps": [ { "picture": "title.cover" } ] }""", "plays_on and until_step")]
    public void BadTitleSequence_IsRefused(string sequence, string why)
    {
        var dir = Directory.CreateTempSubdirectory("p-title-");
        try
        {
            var graphics = Directory.CreateDirectory(Path.Combine(dir.FullName, "game", "graphics"));
            var root = Directory.CreateDirectory(Path.Combine(dir.FullName, "art"));
            File.WriteAllText(Path.Combine(graphics.FullName, "index.json"),
                """{ "game": "game", "default_order": ["t"], "packs": ["t.json"] }""");
            File.WriteAllText(Path.Combine(graphics.FullName, "t.json"),
                $$"""{ "id": "t", "game": "game", "platform": "t", "root": {{System.Text.Json.JsonSerializer.Serialize(root.FullName)}}, "title_sequence": {{sequence}} }""");
            var ex = Assert.Throws<InvalidDataException>(() => GraphicsPackCatalog.Load(Path.Combine(dir.FullName, "game")));
            Assert.Contains(why, ex.Message);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void TheTitleTorches_Play_AtTheMeasuredPass_AndOnlyOnTheirStep()
    {
        var pack = DefaultPack(GameId.P4);
        var splash = new TitleSplash(GameId.P4, pack.TitleSequence!) { Animations = pack.PictureAnimations, Panels = pack.CommandPanels };
        var player = splash.Player!;
        splash.Update(1.0, false, false);
        Assert.Null(player.Scene); // "STARCRAFT Presents": no picture yet
        splash.Update(0.016, true, false);
        Assert.Equal("title", player.Scene); // the picture's _setanim; nothing copied on the first frame
        Assert.Empty(player.Shown);
        Assert.Equal(5.83, player.PassMilliseconds);
        for (var i = 0; i < 120; i++)
            splash.Update(1.0 / 60, false, false); // 2 s: a torch frame lasts 26-38 passes (150-220 ms)
        Assert.Contains(27, player.Shown.Keys);
        Assert.Contains(28, player.Shown.Keys);
        splash.Update(0.016, true, false);
        Assert.False(splash.Done); // s151: _disk(0)'s panel waits for its pick; the torches play on under it
        splash.MenuIcons = TitleSplash.DiskMenuIcons(pack.CommandPanels!.DiskMenu!, _ => false);
        Assert.True(splash.Choose("N"));
        Assert.True(splash.Done);
    }

    [OriginalDataFact]
    public void DiskMenu_IsReadFromP4X_NewGameThenTheSaveSlots()
    {
        // _diskmenu (0x1AB4): _mkicon(1, 0x3F5C8, 1, "Select File"), then "*k:Save" (icon 1, key k) for k = 1..3.
        var disk = DefaultPack(GameId.P4).CommandPanels!.DiskMenu!;
        Assert.Equal("Select File", disk.TitleText);
        Assert.Equal(["N", "1", "2", "3"], disk.Icons.Select(i => i.Key));
        Assert.Equal([0, 1, 1, 1], disk.Icons.Select(i => i.Icon));
        Assert.Equal(["N:New Game", "*1:Save", "*2:Save", "*3:Save"], disk.Icons.Select(i => i.Text));
    }

    [OriginalDataFact]
    public void DiskMenu_ShowsNewGame_AndOnlyTheSlotsThatHoldASave_AtTheNextPlaces()
    {
        var set = DefaultPack(GameId.P4).CommandPanels!;
        Assert.Equal(["N"], TitleSplash.DiskMenuIcons(set.DiskMenu!, _ => false).Select(i => i.Key));
        Assert.Equal(["N", "3"], TitleSplash.DiskMenuIcons(set.DiskMenu!, k => k == 3).Select(i => i.Key));
        // mode 0 puts the j-th save shown at x 38 + 24 (j + 1 & 1), y 60 + 52 j: the panels' place j + 1.
        Assert.Equal((62, 60), set.IconAt(1));
        Assert.Equal((38, 112), set.IconAt(2));
    }

    [OriginalDataFact]
    public void DiskMenu_TakesOnlyItsIcons_AndEscSkipsToIt()
    {
        var pack = DefaultPack(GameId.P4);
        var splash = new TitleSplash(GameId.P4, pack.TitleSequence!) { Panels = pack.CommandPanels };
        splash.MenuIcons = TitleSplash.DiskMenuIcons(pack.CommandPanels!.DiskMenu!, k => k == 1);
        Assert.False(splash.Choose("N")); // no menu on "STARCRAFT Presents"
        splash.Update(0.016, false, true); // Esc: to the menu, not past it
        Assert.Equal(1, splash.Step);
        splash.Update(0.016, true, true); // a key or Esc does nothing there
        Assert.False(splash.Done);
        Assert.False(splash.Choose("2")); // no save in slot 2
        Assert.False(splash.Choose(TitleSplash.MenuKey(Microsoft.Xna.Framework.Input.Keys.Space)));
        Assert.True(splash.Choose(TitleSplash.MenuKey(Microsoft.Xna.Framework.Input.Keys.NumPad1)));
        Assert.Equal("1", splash.MenuPick);
        Assert.True(splash.Done);
    }
}
