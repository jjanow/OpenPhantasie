using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s221: the C64 I / II packs' ending and party-wipe slots (<c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9,
/// <c>…/phantasie2/GRAPHICS.md</c> 8): the layouts the Client's screens need load, the slots resolve to the extract, and
/// what the C64 has no picture for stays a fallback.
/// </summary>
public class C64EndScreenTests
{
    private static GraphicsPack Pack(string path) =>
        GraphicsPackCatalog.Load(path).Find("c64") ?? throw new InvalidOperationException("c64 extracts missing");

    [OriginalDataFact]
    public void P1Pack_DrawsZeus_AndTheDefeatOnDosPages()
    {
        var pack = Pack(GameDataRepository.DefaultPhantasie1Path);
        var ending = pack.EndingScene;
        Assert.NotNull(ending);
        Assert.Equal(16, ending.Palette.Count);
        Assert.EndsWith("ending/zeus.png", pack.Resolve(PictureNames.EndingFinal));
        Assert.EndsWith("ending/sprites/d0-cloud.png", pack.Resolve(ending.Ghost!));
        Assert.EndsWith("ending/sprites/cf-lightning-hand.png", pack.Resolve(ending.Arm!));
        Assert.Equal((178, 12), (ending.ArmX, ending.ArmY)); // sprite 7 at VIC (202, 62)
        Assert.Null(ending.Sounds); // no C64 sounds: the screen's own waits only
        Assert.Null(ending.Crowd);
        Assert.Null(ending.IntroPage);

        // The party wipe: DOS's pages with CEMETARY's pictures (the pack's own DOS-style art).
        Assert.NotNull(pack.DefeatPictures);
        Assert.Null(pack.DefeatScreen); // the Amiga's own defeat screens need its door and timings
        Assert.EndsWith("defeat/temple.png", pack.Resolve(PictureNames.DefeatTemple));
        Assert.EndsWith("defeat/temple-demon.png", pack.Resolve(PictureNames.DefeatDemon));
        Assert.EndsWith("defeat/ghost.png", pack.Resolve(PictureNames.DefeatGhost));
        for (var race = 0; race < 15; race++)
            Assert.EndsWith(".png", pack.Resolve(PictureNames.DefeatFigure(race)));
        Assert.Null(pack.Resolve(PictureNames.DefeatFigure(15))); // CSPRIT holds no UNDEAD figure
        Assert.Equal(DosDefeatArt.Dos, P1DosDefeatScene.ArtFor(
            pack.DefeatPictures is not null && pack.Resolve(PictureNames.DefeatTemple) is not null,
            pack.Resolve(PictureNames.DefeatDemon) is not null, pack.Resolve(PictureNames.DefeatGhost) is not null,
            pack.Resolve(PictureNames.Decor) is not null, pack.Resolve(PictureNames.DecorDoor) is not null,
            pack.Resolve(PictureNames.DecorDemonFace) is not null, pack.Resolve(PictureNames.PortraitGhost) is not null));
        // Not on the C64: a grave, a sorry sign, the Amiga's sinking door.
        Assert.Null(pack.Resolve("combat.grave"));
        Assert.Null(pack.Resolve(PictureNames.DecorSorrySign));
        Assert.Null(pack.Resolve(PictureNames.DecorDoor));
    }

    [OriginalDataFact]
    public void P2Pack_DrawsTheHall_TheCrowd_AndTheTemple()
    {
        var pack = Pack(GameDataRepository.DefaultPathFor(GameId.P2));
        var ending = pack.EndingScene;
        Assert.NotNull(ending);
        Assert.EndsWith("ending/hall.png", pack.Resolve(PictureNames.EndingFinal));
        Assert.NotNull(ending.Crowd);
        Assert.Equal(["ending.crowd.0", "ending.crowd.1"], ending.Crowd.Frames);
        Assert.All(ending.Crowd.Frames, f => Assert.NotNull(pack.Resolve(f)));
        Assert.Equal((0, 110), (ending.Crowd.X, ending.Crowd.Y)); // TLUM1's band at VIC y 160
        // Filmon's pages are kept (without them the screen would drop their lines); no OK form on the C64 (black).
        Assert.NotNull(ending.IntroPage);
        Assert.NotNull(ending.ClosingPage);
        Assert.Equal(0, ending.IntroPage.ButtonPen);
        Assert.True(ending.RowByRecord);
        Assert.Null(ending.Ghost);
        Assert.Null(ending.Arm);

        // The party wipe through II's defeat scene: the temple, the face's doorway at the slot's (123, 19), the ghost.
        Assert.EndsWith("defeat/temple.png", pack.Resolve(PictureNames.Decor));
        Assert.EndsWith("defeat/temple-demon-doorway.png", pack.Resolve(PictureNames.DecorDemonFace));
        Assert.EndsWith("defeat/ghost.png", pack.Resolve(PictureNames.PortraitGhost));
        Assert.Null(pack.Resolve(PictureNames.DecorDoor)); // the C64's door rises: not modelled
        Assert.Null(pack.Resolve(PictureNames.DecorSorrySign));
        Assert.Null(pack.DefeatPictures);
    }
}
