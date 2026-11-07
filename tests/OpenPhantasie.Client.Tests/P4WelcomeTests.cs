using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>s151: <c>_tload</c>'s "Welcome!" (FORMAT.md 6.10): the pack's measured hold and the loads that start it.</summary>
public class P4WelcomeTests
{
    [OriginalDataFact]
    public void Welcome_IsThePacksMeasuredHold_ColourOneBack_ItalicColour15At156x81()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;
        var w = pack.PictureAnimations!.Welcome!;
        Assert.Equal((156, 81, 1, 15), (w.X, w.Y, w.Back, w.Colour));
        Assert.Equal(1137, w.HoldMilliseconds); // XEiJ: TOWN_1.PKH read 282.5 ms + decoded 854.0 ms
        Assert.Equal(319, w.CoverMilliseconds);
    }

    [OriginalDataFact]
    public void PleaseWait_ComesFirst_GreyBack_SunkenBox_Font0Colour0_TheMeasuredHolds()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;
        var p = pack.PictureAnimations!.Welcome!.PleaseWait!;
        Assert.Equal((156, 81, 14, 0), (p.X, p.Y, p.Back, p.Colour)); // XEiJ shot s156
        Assert.Equal((4, 80, 304, 16, 1, 15), (p.BoxX, p.BoxY, p.BoxWidth, p.BoxHeight, p.Dark, p.Light)); // _whole
        Assert.Equal(1374, P4DungeonScreen.PleaseWaitHold(p, ending: false)); // AN sheet 283.4 + 592.4 + 222.6, _wcls 274.5
        Assert.Equal(275, P4DungeonScreen.PleaseWaitHold(p, ending: true));   // _tload(1): no sheet, the _wcls part
        Assert.Equal(0, P4DungeonScreen.PleaseWaitHold(null, ending: false));
    }

    [Theory]
    [InlineData(true, true, false, true)]   // a new game or a load on the street
    [InlineData(true, false, false, false)] // a new loop at an entry prompt: its own _tload comes later
    [InlineData(false, false, true, true)]  // an arrival's _tload
    [InlineData(false, true, false, false)] // the same street, nothing loaded
    public void Welcome_StartsOnEachStreetLoad_NotAtAnEntryPrompt(bool newLoop, bool street, bool loaded, bool starts) =>
        Assert.Equal(starts, P4DungeonScreen.WelcomeStarts(newLoop, street, loaded));

    [OriginalDataFact]
    public void EveryStreetLoad_IsCounted_NewGameAndEachArrival()
    {
        var game = Phantasie4World.NewGame(GameOptions.ForPreset(GameId.P4), new SeededRandomSource(1));
        Assert.Equal(1, game.Town.Loads); // New Game: _town -> _tload in Pendragon
        game.Town.Arrive(P4TownLoop.Pendragon);
        Assert.Equal(2, game.Town.Loads);
    }
}
