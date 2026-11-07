using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s109 (II ST live, <c>p2-town-01-pippacott-new-game.png</c>): a new turn opens Pippacott with the town global still 0,
/// so the banner is black until the party next enters a town. <see cref="GameSession.FreshStart"/> carries that state.
/// </summary>
public class FreshStartTests
{
    [OriginalDataFact]
    public void NewGame_IsFresh_UntilTheNextTownEntry()
    {
        var s = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(3));
        Assert.False(s.FreshStart);
        s.StartNewGame();
        Assert.True(s.FreshStart);
        s.OpenGuild();
        s.LeaveGuild();
        Assert.True(s.FreshStart); // the buildings of the first town keep it
        s.EnterTown(2);
        Assert.False(s.FreshStart);
    }
}
