using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s157, I's party defeat with pictures on DOS's 5.00-s line clock (OV1 <c>0x2af</c> / <c>0x3bc</c>, endgame.md section 4;
/// the s63 DOSBox shots): the pictures follow the art pack (user, s157): the CGA pack's DOS pictures, the Amiga pack's own.
/// </summary>
public class P1DosDefeatSceneTests
{
    private static readonly GraphicsPackCatalog Catalog = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1));

    private static PartyDefeatResult Defeat() => new()
    {
        Fates = [(new Character { Name = "A", Race = Race.Human }, DefeatFate.Destroyed),
                 (new Character { Name = "B", Race = Race.Undead }, DefeatFate.Undead),
                 (new Character { Name = "C", Race = Race.Elf }, DefeatFate.Resurrected)],
        Log = ["YOUR PARTY HAS LOST THE FIGHT", "YOU HAVE REACHED THE MISTY GATEWAY", "THE OLD HERMIT STEPS UP AND SAYS:",
               "A YOU ARE GONE FOR GOOD", "B YOU WALK AS THE DEAD", "C YOU ARE WHOLE AGAIN"],
    };

    // The outcome prompt's lines (SessionPrompts: the log without the defeat banner).
    private static IReadOnlyList<string> Lines(PartyDefeatResult d) => d.Log.Skip(1).ToList();

    [Fact]
    public void Frames_FollowDosBeats_TempleThenFaceThenOneFatePerLine()
    {
        var d = Defeat();
        var lines = Lines(d);
        Assert.Equal(new DosDefeatFrame(false, 0, "YOU HAVE REACHED THE MISTY GATEWAY"), P1DosDefeatScene.At(0, lines, 3));
        Assert.Equal(new DosDefeatFrame(true, 0, "THE OLD HERMIT STEPS UP AND SAYS:"), P1DosDefeatScene.At(1, lines, 3));
        Assert.Equal(new DosDefeatFrame(true, 1, "A YOU ARE GONE FOR GOOD"), P1DosDefeatScene.At(2, lines, 3)); // fate, then its line
        Assert.Equal(new DosDefeatFrame(true, 3, "C YOU ARE WHOLE AGAIN"), P1DosDefeatScene.At(4, lines, 3));
        Assert.Equal(P1DosDefeatScene.At(4, lines, 3), P1DosDefeatScene.At(5, lines, 3)); // the clock's end keeps the last
    }

    [Fact]
    public void AmigaArt_DoorShutOnTheAstralLine_GoneWithTheFaceAfter()
    {
        var lines = Lines(Defeat());
        Assert.Equal(new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false), P1DosDefeatScene.AmigaDoor(P1DosDefeatScene.At(0, lines, 3)));
        Assert.Equal(new DefeatSceneFrame(0, true), P1DosDefeatScene.AmigaDoor(P1DosDefeatScene.At(1, lines, 3)));
        Assert.Equal(new DefeatSceneFrame(0, true), P1DosDefeatScene.AmigaDoor(P1DosDefeatScene.At(3, lines, 3)));
    }

    [Fact]
    public void Row_GhostsUntilTheFate_DestroyedCleared_UndeadIsRace15()
    {
        var d = Defeat();
        Assert.All(P1DosDefeatScene.Row(d, 0), p => Assert.Equal(DefeatPortraitKind.Ghost, p.Kind));
        var row = P1DosDefeatScene.Row(d, 2);
        Assert.Equal(DefeatPortraitKind.Cleared, row[0].Kind);
        Assert.Equal(new DefeatPortrait(DefeatPortraitKind.Race, 15), row[1]);
        Assert.Equal(DefeatPortraitKind.Ghost, row[2].Kind);
        Assert.Equal(new DefeatPortrait(DefeatPortraitKind.Race, (int)Race.Elf), P1DosDefeatScene.Row(d, 3)[2]);
    }

    [Fact]
    public void Art_FollowsThePack_NeverBorrows()
    {
        Assert.Equal(DosDefeatArt.Dos, P1DosDefeatScene.ArtFor(true, true, true, false, false, false, false));
        Assert.Equal(DosDefeatArt.Dos, P1DosDefeatScene.ArtFor(true, true, true, true, true, true, true)); // own first
        Assert.Equal(DosDefeatArt.Amiga, P1DosDefeatScene.ArtFor(false, false, false, true, true, true, true));
        Assert.Equal(DosDefeatArt.None, P1DosDefeatScene.ArtFor(true, false, true, true, false, true, true));
    }

    [OriginalDataFact]
    public void CgaPack_HasTheDosPictures_Optional_AndTheRowLayout()
    {
        var cga = Catalog.Find("dos-cga") ?? throw new InvalidOperationException("DOS extracts missing");
        var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(GameDataRepository.DefaultPathFor(GameId.P1), "graphics", "dos-cga.json")));
        Assert.DoesNotContain(manifest.RootElement.GetProperty("requires").EnumerateArray(),
            r => r.GetString()!.Contains("defeat", StringComparison.Ordinal)); // optional: the pack works without them
        foreach (var name in new[] { PictureNames.DefeatTemple, PictureNames.DefeatDemon, PictureNames.DefeatGhost,
                     PictureNames.DefeatFigure(0), PictureNames.DefeatFigure(15) })
            Assert.True(File.Exists(cga.Resolve(name)), name);
        Assert.Null(cga.Resolve(PictureNames.DefeatFigure(16)));
        // No Amiga-named defeat art in the CGA pack (the party strip, II's scene and the victory keep resolving to nothing).
        Assert.Null(cga.Resolve(PictureNames.Decor));
        Assert.Null(cga.Resolve(PictureNames.PortraitGhost));
        Assert.Null(cga.Resolve(PictureNames.PortraitIdle(0)));
        Assert.Null(cga.DefeatScreen); // so AMIGA SCREENS on this pack runs DOS's sequence with these pictures
        var l = cga.DefeatPictures!;
        Assert.Equal((156, 152, 191, 192, "#FFFF55"), (l.RowY, l.ClearTop, l.ClearBottom, l.TextY, l.TextColour));
        Assert.Equal(["#000000", "#55FF55", "#FF5555", "#FFFF55"], l.Palette!); // OV1 0x2c7: CGA palette 0 high
    }

    [OriginalDataFact]
    public void CgaSlots_AreOv1s_GapRoundedDownToFourPixels()
    {
        var l = Catalog.Find("dos-cga")!.DefeatPictures!;
        // 6 members: (320 - 192) / 7 = 18 -> 16; slots 16, 64, ..., 256 (the s63 shot's six ghosts).
        Assert.Equal([16, 64, 112, 160, 208, 256], Enumerable.Range(0, 6).Select(i => l.SlotX(6, i)));
        Assert.Equal(144, l.SlotX(1, 0)); // (320 - 32) / 2 = 144
        Assert.Equal([84, 200], Enumerable.Range(0, 2).Select(i => l.SlotX(2, i))); // 256 / 3 = 85 -> 84
    }

    [OriginalDataFact]
    public void AmigaPack_HasNoDosPictures_ButItsOwnDefeatArt()
    {
        var amiga = Catalog.Find("amiga") ?? throw new InvalidOperationException("Amiga extracts missing");
        Assert.Null(amiga.Resolve(PictureNames.DefeatTemple));
        Assert.Null(amiga.Resolve(PictureNames.DefeatGhost));
        Assert.Null(amiga.DefeatPictures);
        foreach (var name in new[] { PictureNames.Decor, PictureNames.DecorDoor, PictureNames.DecorDemonFace, PictureNames.PortraitGhost })
            Assert.True(File.Exists(amiga.Resolve(name)), name);
    }
}
