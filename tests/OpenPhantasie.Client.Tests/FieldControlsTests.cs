using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// D-076 / D-077 (manual WILDERNESS TRAVEL): the 3/N E S W moves and C H I U T commands, the numpad (diagonals as a QoL
/// switch), and the party pictures + command list beside the map.
/// </summary>
public class FieldControlsTests
{
    [Fact]
    public void Defaults_AreTheManualsKeys_D076()
    {
        var s = new ClientSettings();
        Assert.Equal(Keys.N, s.GetKey(GameAction.MoveNorth));
        Assert.Equal(Keys.E, s.GetKey(GameAction.MoveEast));
        Assert.Equal(Keys.S, s.GetKey(GameAction.MoveSouth));
        Assert.Equal(Keys.W, s.GetKey(GameAction.MoveWest));
        Assert.Equal(Keys.C, s.GetKey(GameAction.CastSpell));
        Assert.Equal(Keys.H, s.GetKey(GameAction.Health));
        Assert.Equal(Keys.I, s.GetKey(GameAction.Inspect));
        Assert.Equal(Keys.U, s.GetKey(GameAction.UsePotion));
        Assert.Equal(Keys.T, s.GetKey(GameAction.TimeLag));
        Assert.Equal(Keys.L, s.GetKey(GameAction.Look)); // dungeon only (manual DUNGEON EXPLORATION)
        Assert.Equal(Keys.None, s.GetKey(GameAction.TurnRight)); // E is east now
    }

    [Fact]
    public void OldSettingsFile_GetsTheNewMoveKeys_ButKeepsOtherRemaps()
    {
        var s = ClientSettings.FromJson("""
            { "fullScreenMap": true,
              "keyBindings": { "MoveNorth": "W", "MoveWest": "A", "Inspect": "P", "Save": "F1" } }
            """)!;
        Assert.Equal(Keys.N, s.GetKey(GameAction.MoveNorth));
        Assert.Equal(Keys.W, s.GetKey(GameAction.MoveWest));
        Assert.Equal(Keys.I, s.GetKey(GameAction.Inspect));
        Assert.Equal(Keys.F1, s.GetKey(GameAction.Save));
        Assert.Equal(ClientSettings.CurrentBindingsVersion, s.BindingsVersion);
        Assert.False(s.MapOnly); // D-077: the manual's screen, whatever the old full-screen flag said
    }

    [Fact]
    public void CurrentSettingsFile_KeepsItsMoveRemaps() =>
        Assert.Equal(Keys.Up, ClientSettings.FromJson(
            $$"""{ "bindingsVersion": {{ClientSettings.CurrentBindingsVersion}}, "keyBindings": { "MoveNorth": "Up" } }""")!
            .GetKey(GameAction.MoveNorth));

    [Fact]
    public void QoLSwitches_StartAsAsked()
    {
        var s = new ClientSettings();
        Assert.False(s.Game(GameId.P1).StatusLines); // off = manual
        Assert.True(s.Game(GameId.P1).DiagonalMoves); // the user asked for numpad diagonals
        Assert.False(s.MapOnly);
    }

    private static (int, int)? Press(bool diagonals, params Keys[] keys)
    {
        var input = new InputMap(new ClientSettings());
        input.EndFrame(new KeyboardState(), default);
        return input.PressedFieldMove(new KeyboardState(keys), default, diagonals);
    }

    [Theory]
    [InlineData(Keys.D3, 0, -1)]
    [InlineData(Keys.N, 0, -1)]
    [InlineData(Keys.E, 1, 0)]
    [InlineData(Keys.S, 0, 1)]
    [InlineData(Keys.W, -1, 0)]
    [InlineData(Keys.Up, 0, -1)]
    [InlineData(Keys.Left, -1, 0)]
    [InlineData(Keys.NumPad8, 0, -1)]
    [InlineData(Keys.NumPad2, 0, 1)]
    [InlineData(Keys.NumPad4, -1, 0)]
    [InlineData(Keys.NumPad6, 1, 0)]
    public void StraightMoves(Keys key, int dx, int dy)
    {
        Assert.Equal((dx, dy), Press(false, key));
        Assert.Equal((dx, dy), Press(true, key));
    }

    [Theory]
    [InlineData(Keys.NumPad7, -1, -1)]
    [InlineData(Keys.NumPad9, 1, -1)]
    [InlineData(Keys.NumPad1, -1, 1)]
    [InlineData(Keys.NumPad3, 1, 1)]
    [InlineData(Keys.Home, -1, -1)]
    [InlineData(Keys.PageDown, 1, 1)]
    public void Diagonals_OnlyWithTheSwitch(Keys key, int dx, int dy)
    {
        Assert.Equal((dx, dy), Press(true, key));
        Assert.Null(Press(false, key));
    }

    [Fact]
    public void ADoesNotMove() => Assert.Null(Press(true, Keys.A));

    [Fact]
    public void Diagonal_IsNorthSouthThenEastWest()
    {
        Assert.Equal([CardinalDirection.North, CardinalDirection.East], InputMap.StepsFor(1, -1));
        Assert.Equal([CardinalDirection.South, CardinalDirection.West], InputMap.StepsFor(-1, 1));
        Assert.Equal([CardinalDirection.West], InputMap.StepsFor(-1, 0));
    }

    [Fact]
    public void Sidebar_PicturesAndCommands_FitBesideTheMap_D077()
    {
        var area = UiOverlays.FieldPartyArea;
        Assert.True(area.X >= 4 + 13 * 12, "right of the 13-cell wilderness window");
        foreach (var commands in new[] { UiOverlays.FieldCommands, UiOverlays.DungeonFieldCommands })
        {
            Assert.True(UiOverlays.FieldCommandsTop + commands.Length * PixelFont.LineHeight <= UiOverlays.ContentBottom);
            Assert.All(commands, l => Assert.True(PixelFont.Measure(l) <= area.Width, l));
        }
        Assert.Contains(UiOverlays.DungeonFieldCommands, l => l.Contains("L)OOK"));
        Assert.DoesNotContain(UiOverlays.FieldCommands, l => l.Contains("L)OOK"));
        var layout = PartyStripLayout.Compute(PartyStripLayout.MaxCards, area, 50);
        Assert.False(layout.Lines); // seven portrait cards still fit in two rows
    }

    [Fact]
    public void SettingsBeforeT50_MoveTheQoLSwitchesIntoGameI()
    {
        var s = ClientSettings.FromJson("""{ "numpadDiagonals": false, "statusLines": true }""")!;
        Assert.False(s.Game(GameId.P1).DiagonalMoves);
        Assert.True(s.Game(GameId.P1).StatusLines);
        Assert.Null(s.LegacyNumpadDiagonals);
        Assert.Null(s.LegacyStatusLines);
    }
}
