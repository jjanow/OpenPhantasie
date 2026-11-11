using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

public class PadFixListGTests
{
    [OriginalDataTheory]
    [InlineData(P4Building.Inn)]
    [InlineData(P4Building.Mystic)]
    public void SimpleBuildingWait_RefusesSave_AndHelpAgrees(P4Building building)
    {
        var game = P4Preview.StartInTown(2, GameOptions.ForPreset(GameId.P4, OptionPreset.Dos));
        var screen = new P4DungeonScreen(game);
        game.Town.Enter(building);
        Assert.Equal(P4TownPrompt.Wait, game.Town.Prompt);
        Assert.True(game.Town.InBuilding);
        Assert.False(game.CanSave);
        Assert.Throws<InvalidOperationException>(() => game.Capture());
        var was = PadWording.Active;
        try
        {
            PadWording.Active = true;
            Assert.DoesNotContain("LB save", screen.Help);
            Assert.Null(screen.HandleKeys([Keys.F5]));
        }
        finally { PadWording.Active = was; }
        game.Town.Continue();
        Assert.False(game.Town.InBuilding);
        Assert.True(game.CanSave);
    }

    [Fact]
    public void PictureBlocks_KeepTheirHeaderAndArtTogether_ThroughBothEnds()
    {
        var scroll = new ScrollBox();
        int[] heights = [34, 80, 32, 72];
        Assert.Equal([(0, 0)], UiOverlays.WholePictureRows(scroll, heights, 90));
        scroll.ScrollBy(1);
        Assert.Equal([(1, 0)], UiOverlays.WholePictureRows(scroll, heights, 90));
        scroll.ScrollBy(1);
        Assert.Equal([(2, 0)], UiOverlays.WholePictureRows(scroll, heights, 90));
        scroll.End();
        Assert.Equal([(3, 0)], UiOverlays.WholePictureRows(scroll, heights, 90));
        scroll.Home();
        Assert.Equal([(0, 0), (1, 36)], UiOverlays.WholePictureRows(scroll, heights, 116));
        Assert.Equal([(0, 0)], UiOverlays.WholePictureRows(scroll, heights, 115));
    }

    [Fact]
    public void PictureBlocks_AtAnExactFit_KeepTheWholePicture()
    {
        var scroll = new ScrollBox();
        Assert.Equal([(0, 0)], UiOverlays.WholePictureRows(scroll, [80, 34], 80));
        scroll.ScrollBy(1);
        Assert.Equal([(1, 0)], UiOverlays.WholePictureRows(scroll, [80, 34], 80));
        Assert.Empty(UiOverlays.WholePictureRows(scroll, [], 80));
    }
}
