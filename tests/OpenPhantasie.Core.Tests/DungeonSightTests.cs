using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s67: the dungeon step reveal OV2 0x20e5 and L)OOK 0x1f9f — [C] static (dungeon-representation.md §Sight).
/// </summary>
public class DungeonSightTests
{
    private static DungeonLevel Filled(byte cell)
    {
        var cells = new byte[DungeonLevel.GridByteLength];
        Array.Fill(cells, cell);
        return new DungeonLevel(1, "TEST", cells);
    }

    [Fact]
    public void Step_DrawsThe3x3Box_ClippedToTheLevel()
    {
        var level = Filled(7);
        Assert.Equal(9, DungeonSight.StepReveal(level, 16, 18).Count);
        Assert.Equal(4, DungeonSight.StepReveal(level, 0, 0).Count);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0xb9)]
    [InlineData(0xc7)]
    public void Step_OnCorridorWhiteOrLockBank1_DrawsOnlyItsOwnCell(byte cell)
    {
        var level = Filled(7);
        level.Set(16, 18, cell);
        Assert.Equal([(16, 18)], DungeonSight.StepReveal(level, 16, 18));
    }

    [Fact]
    public void Step_OnLockBank2_StillDrawsTheBox()
    {
        var level = Filled(7);
        level.Set(16, 18, 0xc8);
        Assert.Equal(9, DungeonSight.StepReveal(level, 16, 18).Count);
    }

    [Fact]
    public void Look_InOpenFloor_DrawsThe9x9Square()
    {
        var cells = DungeonSight.Look(Filled(7), 16, 18)!;
        Assert.Equal(81, cells.Count);
        Assert.Equal(12, cells.Min(c => c.X));
        Assert.Equal(20, cells.Max(c => c.X));
        Assert.Equal(14, cells.Min(c => c.Y));
        Assert.Equal(22, cells.Max(c => c.Y));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0xb9)]
    [InlineData(0xd6)]
    public void Look_OnCorridorWhiteOrDoorCells_DoesNothing(byte cell)
    {
        var level = Filled(7);
        level.Set(16, 18, cell);
        Assert.Null(DungeonSight.Look(level, 16, 18));
    }

    [Theory]
    [InlineData(0xd7)] // exit
    [InlineData(0xdd)] // scripted event
    public void Look_WorksOnExitAndEventCells(byte cell)
    {
        var level = Filled(7);
        level.Set(16, 18, cell);
        Assert.Equal(81, DungeonSight.Look(level, 16, 18)!.Count);
    }

    [Fact]
    public void Look_RayStopsAtABlockingCell_WhichIsStillDrawn()
    {
        var level = Filled(7);
        level.Set(17, 18, 0xb9); // a door east of the party
        var cells = DungeonSight.Look(level, 16, 18)!;
        Assert.Contains((17, 18), cells); // in the party's own box
        Assert.Equal(72, cells.Count); // the east ray's box (x 18..20, y 17..19) is never drawn
        Assert.DoesNotContain((18, 18), cells);
        Assert.Contains((20, 16), cells); // the north-east ray's box
    }

    [Fact]
    public void Look_DiagonalRayHasNoCornerCheck()
    {
        var level = Filled(0);
        level.Set(16, 18, 7);
        level.Set(17, 17, 7);
        level.Set(18, 16, 7);
        level.Set(19, 15, 7); // a diagonal of floor between walls that touch at the corners
        Assert.Contains((20, 14), DungeonSight.Look(level, 16, 18)!);
    }

    [OriginalDataFact]
    public void Look_Dng1_FromTheMessageCell20_13()
    {
        var level = GameDataRepository.LoadPhantasie1().CreateDungeonTraversal().GetLevel(1);
        Assert.Equal(0x8c, level.Get(20, 13));
        var cells = DungeonSight.Look(level, 20, 13)!;
        Assert.Equal(54, cells.Count); // vs 9 for a step
        Assert.Equal(10, cells.Min(c => c.Y));
        Assert.Contains((16, 17), cells); // seen through the gaps of the wall row y = 15
        Assert.DoesNotContain((15, 15), cells);
    }
}
