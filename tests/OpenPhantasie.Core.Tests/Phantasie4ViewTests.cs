using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// What IV's pictures need from Core (T6.4, s108; <c>FORMAT.md</c> §6.9): the location-map view's window, sight and
/// doorway marks (<see cref="P4DungeonView"/>: <c>_idcopy</c>, <c>_calim</c>, <c>_prex</c>) and the combatants' facing
/// (<see cref="P4BattleFacing"/>: <c>_b_face</c>, <c>_b_chkdir</c>, <c>_dtb</c>).
/// </summary>
public class Phantasie4ViewTests
{
    private const int S = P4DungeonView.Size;

    private static ushort Word(int floor, int north, int west) => (ushort)(floor | (north << 6) | (west << 11));

    private static P4DungeonMap Map(Action<ushort[]>? edit = null)
    {
        var words = new ushort[31 * 31];
        Array.Fill(words, Word(2, 31, 7));
        edit?.Invoke(words);
        return new P4DungeonMap(new P4DungeonFile(9, "test", words, [new(0x20, 0, 4, 0)], [0x22, 0x00]));
    }

    private static int[] Open() => Enumerable.Repeat((int)Word(2, 31, 7), S * S).ToArray();

    [Fact]
    public void Window_CentredOnTheParty_OutsideCellsAre0xFFFF()
    {
        var view = P4DungeonView.Of(Map(w => w[10 * 31 + 12] = Word(5, 1, 2)), 12, 10, 0, false);
        Assert.Equal(Word(5, 1, 2), view.Cell(4, 4));
        var corner = P4DungeonView.Of(Map(), 0, 0, 0, false);
        Assert.Equal(P4DungeonView.Outside, corner.Cell(3, 4));
        Assert.Equal(P4DungeonView.Outside, corner.Cell(4, 3));
        Assert.NotEqual(P4DungeonView.Outside, corner.Cell(4, 4));
        var far = P4DungeonView.Of(Map(), 29, 29, 0, false);
        Assert.Equal(P4DungeonView.Outside, far.Cell(5, 4)); // x 30 is the walls row/column, outside the map
    }

    [Fact]
    public void Calim_OpenFloor_SeesEverything()
    {
        Assert.All(P4DungeonView.Calim(Open()), Assert.True);
    }

    [Fact]
    public void Calim_RoomWalls_StopSight()
    {
        // A 3 × 3 room around the centre: north walls on row 3 (its top) and row 6 (below it), west walls on column 3 and 6.
        var cells = Open();
        for (var i = 0; i < S; i++)
        {
            cells[3 * S + i] = Word(2, 1, 7);
            cells[6 * S + i] = Word(2, 1, 7);
        }
        for (var r = 0; r < S; r++)
        {
            cells[r * S + 3] = (cells[r * S + 3] & ~(7 << 11)) | (1 << 11);
            cells[r * S + 6] = (cells[r * S + 6] & ~(7 << 11)) | (1 << 11);
        }
        var seen = P4DungeonView.Calim(cells);
        for (var r = 0; r < S; r++)
            for (var c = 0; c < S; c++)
                Assert.Equal(r is >= 3 and <= 5 && c is >= 3 and <= 5, seen[r * S + c]);
    }

    [Fact]
    public void Calim_Grate_PassesSightNorthOnly()
    {
        var north = Open();
        north[4 * S + 4] = Word(2, P4DungeonView.Grate, 7); // grate on the centre's north side
        for (var c = 0; c < S; c++)
            if (c != 4)
                north[4 * S + c] = Word(2, 1, 7);
        Assert.True(P4DungeonView.Calim(north)[3 * S + 4]);

        var south = Open();
        south[5 * S + 4] = Word(2, P4DungeonView.Grate, 7); // grate on the centre's south side
        for (var c = 0; c < S; c++)
            if (c != 4)
                south[5 * S + c] = Word(2, 1, 7);
        Assert.False(P4DungeonView.Calim(south)[5 * S + 4]);
    }

    [Fact]
    public void Prex_MarksDoorwayEdges_AtLoadOnly()
    {
        var map = Map(w =>
        {
            w[6 * 31 + 3] = Word(2, 16, 7); // (3,6) north picture 16 → (3,5) bit 14
            w[5 * 31 + 8] = Word(2, 31, 5); // (8,5) west picture 5 → (7,5) bit 15
            w[5 * 31 + 1] = Word(2, 31, 1); // west picture 1 (a wall) → no mark on (0,5)
        });
        Assert.Equal(P4DungeonView.SouthMark, map.EdgeMarks[5 * 30 + 3]);
        Assert.Equal(P4DungeonView.EastMark, map.EdgeMarks[5 * 30 + 7]);
        Assert.Equal(0, map.EdgeMarks[5 * 30 + 0]);
        map.SetNorthPicture(3, 6, 31);
        Assert.Equal(P4DungeonView.SouthMark, map.EdgeMarks[5 * 30 + 3]); // events do not redo _prex
        Assert.Equal(P4DungeonView.SouthMark, P4DungeonView.Of(map, 3, 5, 0, false).Cell(4, 4) & 0xc000);
    }

    [Theory]
    [InlineData(2, 0, false, 0)]
    [InlineData(2, 3, false, 3)]
    [InlineData(27, 1, false, 5)] // water: + 4
    [InlineData(30, 2, false, 6)]
    [InlineData(28, 2, true, 2)]  // Levitation: walking figure
    public void Marker_FacingPlusFourInWater(int floor, int dir, bool levitating, int expected)
    {
        var map = Map(w => w[10 * 31 + 10] = Word(floor, 31, 7));
        Assert.Equal(expected, P4DungeonView.Of(map, 10, 10, dir, levitating).Marker);
    }

    [OriginalDataFact]
    public void ShippedMaps_UseOnlyPicturesTheFilesHold()
    {
        // fl.pix 44 tiles, yoko.pix 20 (31 = none), tate.pix 6 (7 = none): every index the maps use is inside the files.
        foreach (var f in Phantasie4World.LoadDungeonFiles().Values)
            foreach (var w in f.Words)
            {
                Assert.InRange(P4DungeonView.Floor(w), 0, 43);
                Assert.True(P4DungeonView.North(w) is < 20 or 31);
                Assert.True(P4DungeonView.West(w) is < 6 or 7);
            }
    }

    // --- Facing (_b_face, _b_chkdir, _dtb) ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0, -7, 1)]
    [InlineData(0, -8, 2)]
    [InlineData(0, -1, 2)]
    [InlineData(0, 1, 3)]
    [InlineData(0, -6, 3)]
    [InlineData(0, 7, 0)]
    [InlineData(0, 8, 0)]
    [InlineData(1, 7, 0)]
    [InlineData(1, -1, 2)]
    [InlineData(1, 6, 2)]
    [InlineData(1, 1, 3)]
    [InlineData(1, 8, 3)]
    [InlineData(1, -7, 1)]
    [InlineData(2, -6, 1)]
    [InlineData(2, -7, 1)]
    [InlineData(2, 7, 0)]
    [InlineData(2, 8, 0)]
    [InlineData(2, 1, 3)]
    [InlineData(2, 6, 2)]
    [InlineData(3, -1, 2)]
    [InlineData(3, -7, 1)]
    [InlineData(3, -8, 1)]
    [InlineData(3, 6, 0)]
    [InlineData(3, 7, 0)]
    [InlineData(3, 8, 3)]
    public void Turn_AsBFace(int facing, int delta, int expected) => Assert.Equal(expected, P4BattleFacing.Turn(facing, delta));

    [Theory]
    [InlineData(24, 17, -7)]
    [InlineData(24, 31, 7)]
    [InlineData(24, 25, 1)]
    [InlineData(24, 23, -1)]
    [InlineData(24, 48, 8)]
    [InlineData(24, 4, -6)]
    [InlineData(24, 42, 6)]
    [InlineData(24, 0, -8)]
    [InlineData(24, 24, 0)]
    public void Toward_AsBChkdir(int from, int to, int expected) => Assert.Equal(expected, P4BattleFacing.Toward(from, to));

    [Fact]
    public void Frame_StandingPoseByFacing()
    {
        Assert.Equal((0, false), P4BattleFacing.Frame(P4BattleFacing.Front));
        Assert.Equal((4, false), P4BattleFacing.Frame(P4BattleFacing.Back));
        Assert.Equal((2, false), P4BattleFacing.Frame(P4BattleFacing.Left));
        Assert.Equal((2, true), P4BattleFacing.Frame(P4BattleFacing.Right));
        Assert.Equal((4, true), P4BattleFacing.Frame(P4BattleFacing.Back, 1));
    }

    [OriginalDataFact]
    public void Battle_StartFacings_TerrainAndStepsTurn()
    {
        var t = GameTables.Load(GameId.P4).Phantasie4!.Tables;
        var data = Phantasie4World.LoadBattleData();
        var b = P4Battle.Dungeon(GameOptions.ForPreset(GameId.P4), t, data, Phantasie4BattleLeftoversTests.Party(members: 1),
            new SeededRandomSource(3), 0, terrain: 1);
        Assert.Equal(1, b.Terrain);
        Assert.All(b.Members, m => Assert.Equal(P4BattleFacing.Back, m.Facing));
        Assert.All(b.Monsters, m => Assert.Equal(P4BattleFacing.Front, m.Facing));
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        var m = b.Members[0];
        for (var dir = 0; dir < 8; dir++)
        {
            var (from, facing) = (m.Square, m.Facing);
            if (!b.Step(dir))
                continue;
            Assert.Equal(P4BattleFacing.Turn(facing, m.Square - from), m.Facing);
            return;
        }
        Assert.Fail("no step possible");
    }
}
