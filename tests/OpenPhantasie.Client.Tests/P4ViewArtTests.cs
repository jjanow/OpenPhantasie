using System.Buffers.Binary;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// IV's location-map view and combat field with the X68000 pack (T6.4, s108; <c>FORMAT.md</c> §6.9): every picture the
/// maps, fields and fighters can name exists at its decoded size, and both pictures fit beside the text.
/// </summary>
public class P4ViewArtTests
{
    private static readonly GraphicsPack Pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;

    private static (int W, int H) PngSize(string path)
    {
        var head = File.ReadAllBytes(path).AsSpan(16, 8);
        return (BinaryPrimitives.ReadInt32BigEndian(head), BinaryPrimitives.ReadInt32BigEndian(head[4..]));
    }

    private static void All(Func<int, string> name, int count, (int, int) size)
    {
        for (var i = 0; i < count; i++)
        {
            var path = Pack.Resolve(name(i));
            Assert.True(path is not null, name(i));
            Assert.Equal(size, PngSize(path!));
        }
        Assert.Null(Pack.Resolve(name(count)));
    }

    [OriginalDataFact]
    public void DungeonPictures_AllThereAtTheirSizes()
    {
        All(P4DungeonScreen.FloorPicture, 44, (32, 32));
        All(P4DungeonScreen.NorthWallPicture, 20, (48, 32));
        All(P4DungeonScreen.WestWallPicture, 6, (16, 64));
        All(P4DungeonScreen.PartyPicture, 8, (32, 32));
    }

    [OriginalDataFact]
    public void CombatPictures_AllThereAtTheirSizes()
    {
        All(P4DungeonScreen.ObstaclePicture, 11, (48, 48));
        All(P4DungeonScreen.SpritePicture, 120, (5 * 48, 48));
        All(i => $"p4.combat.missile.{i}", 15, (32, 32));
        var fields = Phantasie4World.LoadBattleData().Tables.Fields.Select(f => f.Name)
            .Select(n => $"p4.combat.ground.{n}").Concat(["p4.combat.ground.location.0", "p4.combat.ground.location.1"]);
        foreach (var name in fields)
            Assert.Equal((48, 40), PngSize(Pack.Resolve(name) ?? throw new Xunit.Sdk.XunitException(name)));
        // _e_finit's records: location map 3 (grass) or 8 (_etile 1); outdoors per field.
        Assert.EndsWith("08.png", Pack.Resolve("p4.combat.ground.location.1"));
        Assert.EndsWith("03.png", Pack.Resolve("p4.combat.ground.location.0"));
        Assert.EndsWith("00.png", Pack.Resolve("p4.combat.ground.ship"));
    }

    [OriginalDataFact]
    public void ViewAndField_FitBesideTheText()
    {
        // DrawBeside: the picture at layout (4, 4), half a layout pixel per pack pixel, must end by x 200 and y 196.
        Assert.Equal((304, 320), (P4DungeonScreen.ViewWidth, P4DungeonScreen.ViewHeight));
        Assert.Equal((336, 300), (P4DungeonScreen.FieldWidth, P4DungeonScreen.FieldHeight));
        Assert.True(4 + P4DungeonScreen.ViewWidth / 2 <= 200 && 4 + P4DungeonScreen.ViewHeight / 2 <= 196);
        Assert.True(4 + P4DungeonScreen.FieldWidth / 2 <= 200 && 4 + P4DungeonScreen.FieldHeight / 2 <= 196);
        Assert.True(P4DungeonScreen.FieldStrip >= P4DungeonScreen.FigureLift); // row 0's figures stay inside the picture
        Assert.Equal(16, Pack.Palettes["pkh"].Count);
    }

    [Theory]
    [InlineData(P4Ids.Human, 0, null, 84)]
    [InlineData(P4Ids.Dwarf, 1, null, 85)]
    [InlineData(34, 2, null, 81)]
    [InlineData(33, 6, 0, 117)]
    [InlineData(33, 6, 2, 119)]
    [InlineData(33, 6, 3, 117)]
    [InlineData(P4Ids.Human, 3, 2, 84)] // only slot 6 adds the elemental kind
    public void PersonRecord_RacePlus84(int race, int slot, int? elemental, int expected) =>
        Assert.Equal(expected, P4DungeonScreen.PersonRecord(race, slot, elemental));

    [OriginalDataFact]
    public void Preview_DungeonViewAndFightGround()
    {
        var options = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
        var s = new P4DungeonScreen(P4Preview.Start(4, options));
        var view = s.View;
        Assert.NotNull(view);
        Assert.True(view!.IsSeen(P4DungeonView.Centre, P4DungeonView.Centre));
        Assert.InRange(view.Marker, 0, 7);

        var t = GameTables.Load(GameId.P4).Phantasie4!.Tables;
        var b = P4Battle.Dungeon(options, t, Phantasie4World.LoadBattleData(), s.Game.Party, new SeededRandomSource(1), 0, 1);
        Assert.Equal("p4.combat.ground.location.1", P4DungeonScreen.GroundPicture(b));
        Assert.True(P4DungeonScreen.ShowsExitWindow(b));
    }

    [OriginalDataFact]
    public void ViewCycling_PicturesExist_AndNoPackMeansNoAnimator()
    {
        // Every picture _ianim can step to (s120, the pack's dungeon_view_animation) is in the pack.
        var rules = Pack.DungeonViewAnimation!;
        foreach (var c in rules.FloorCycles.Append(rules.RandomFloor))
            for (var n = c.First; n <= c.Last; n++)
                Assert.NotNull(Pack.Resolve(P4DungeonScreen.FloorPicture(n)));
        foreach (var c in rules.NorthCycles)
            for (var n = c.First; n <= c.Last; n++)
                Assert.NotNull(Pack.Resolve(P4DungeonScreen.NorthWallPicture(n)));
        foreach (var c in rules.WestCycles)
            for (var n = c.First; n <= c.Last; n++)
                Assert.NotNull(Pack.Resolve(P4DungeonScreen.WestWallPicture(n)));

        var s = new P4DungeonScreen(P4Preview.Start(1, GameOptions.ForPreset(GameId.P4, OptionPreset.Dos)));
        s.Tick(1000);
        Assert.Null(s.ViewAnimation);
    }
}
