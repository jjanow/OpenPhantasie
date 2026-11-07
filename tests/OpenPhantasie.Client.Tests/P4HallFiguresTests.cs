using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>s151: the Hall stage's import figures (P4.X <c>_hpix</c>, FORMAT.md 6.9 "p4mon.pix").</summary>
public class P4HallFiguresTests
{
    [Fact]
    public void HallRecord_IsRacePlus84_WithoutTheRace34Exception()
    {
        Assert.Equal(84, P4DungeonScreen.HallRecord(0));
        Assert.Equal(98, P4DungeonScreen.HallRecord(14));
        Assert.Equal(117, P4DungeonScreen.HallRecord(P4Hall.UnknownRace)); // "?" draws the Mud elemental's record
        Assert.Equal(81, P4DungeonScreen.PersonRecord(34, 0, null)); // the party's exception stays the party's
        Assert.Equal(118, P4DungeonScreen.HallRecord(34));
    }

    [Fact]
    public void HallFigures_StandAfterThePaintedThree_ThenInASecondRow()
    {
        Assert.Null(P4DungeonScreen.HallFigureAt(2)); // Lord Wood, Filmon, Nikademus are painted into HALL_B
        Assert.Equal([(151, 209), (199, 209), (247, 209), (23, 259), (71, 259), (119, 259), (167, 259), (215, 259)],
            Enumerable.Range(3, 8).Select(s => P4DungeonScreen.HallFigureAt(s)!.Value));
        Assert.Null(P4DungeonScreen.HallFigureAt(P4Party.HallSlots));
        // Each 48 x 48 figure stays on HALL_B (312 x 110 at y 202 under HALL_A).
        Assert.All(Enumerable.Range(3, 8), s =>
        {
            var (x, y) = P4DungeonScreen.HallFigureAt(s)!.Value;
            Assert.InRange(x, 0, 312 - P4DungeonScreen.FigureSize);
            Assert.InRange(y, 202, 202 + 110 - P4DungeonScreen.FigureSize);
        });
    }
}
