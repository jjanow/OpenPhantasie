using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Data;
using Xunit;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// "Pad coverage" (s182), towns and bars: the DOS town row and lists (I, II, III), WHO IS SHOPPING by trigger, III Amiga's
/// town bar, III Amiga's bar outside the towns and its OK windows, and I Amiga's map strip, driven by pad buttons.
/// </summary>
public sealed class PadTownTests
{
    private static TownChoice? Press(TownMenu menu, Buttons button) => menu.HandleKey(InputMap.MenuPadKey(button));

    [Fact]
    public void TownRow_DPadMovesTheHighlight_AOpensOption_ThenPicksInIt()
    {
        var menu = new TownMenu();
        Assert.Equal(TownMenu.FirstBarCursor, menu.BarCursor); // Guild
        Press(menu, Buttons.DPadRight); // wraps to Option
        Assert.Equal(0, menu.BarCursor);
        Press(menu, Buttons.LeftThumbstickLeft);
        Press(menu, Buttons.DPadRight);
        Assert.Equal(0, menu.BarCursor);
        Assert.Null(Press(menu, Buttons.A));
        Assert.Equal(TownPlace.Option, menu.Place);
        Assert.Equal(TownMenu.FirstOptionCursor, menu.SubCursor); // Save game
        Press(menu, Buttons.DPadUp);
        Assert.Equal(TownChoice.ShowParty, Press(menu, Buttons.A));
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [Fact]
    public void TownRow_AOnGuild_PicksTheGuild()
    {
        var menu = new TownMenu();
        Assert.Equal(TownChoice.Guild, Press(menu, Buttons.A));
    }

    [Fact]
    public void TownRow_ShouldersAndB_DoNothingThere()
    {
        // LB / RB are Save / Load (global) and come as PgUp / PgDn; B is the global back-out, its Esc does nothing here.
        var menu = new TownMenu();
        foreach (var b in new[] { Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.B })
            Assert.Null(Press(menu, b));
        Assert.Equal(TownPlace.Menu, menu.Place);
        Assert.Equal(TownMenu.FirstBarCursor, menu.BarCursor);
        Press(menu, Buttons.DPadLeft);
        Press(menu, Buttons.A); // Inn
        Press(menu, Buttons.DPadLeft);
        Press(menu, Buttons.DPadLeft);
        Press(menu, Buttons.DPadLeft);
        Press(menu, Buttons.DPadLeft); // Bank
        Assert.Null(Press(menu, Buttons.A));
        Assert.Equal(TownPlace.Bank, menu.Place);
        Assert.Null(Press(menu, Buttons.RightShoulder));
        Assert.Equal(TownPlace.Bank, menu.Place);
    }

    [Theory]
    [InlineData(Buttons.LeftTrigger, -1)]
    [InlineData(Buttons.RightTrigger, 1)]
    [InlineData(Buttons.A, 0)]
    [InlineData(Buttons.LeftShoulder, 0)]
    [InlineData(Buttons.X, 0)]
    [InlineData(Buttons.Y, 0)]
    public void Shopper_TriggersStepIt(Buttons button, int step) => Assert.Equal(step, TownMenu.ShopperStep(button));

    [Theory]
    [InlineData(0, 6, 1, 1)]
    [InlineData(5, 6, 1, 0)]
    [InlineData(0, 6, -1, 5)]
    [InlineData(9, 3, 1, 0)] // a row past a shrunken party is clamped first
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, 0, 1, 0)]
    public void Shopper_StepWraps(int row, int count, int step, int expected) =>
        Assert.Equal(expected, TownMenu.StepShopper(row, count, step));

    private static CombatFieldMenu Menu(params string[] actions) =>
        new("t", 0, new Box(0, 0, 1, 1), actions, new Box(0, 0, 1, 1), 10, 0, 0) { Actions = actions };

    /// <summary>III Amiga's town bar: File, Guild, Options.</summary>
    private static CombatFieldMenuBar TownBar() => new(11, 0, 1, 1,
        [Menu("info", "load", "save"), Menu("add", "drop"), Menu("armoury", "bank", "inn", "mystic", "exit")]);

    private static (bool Used, string Action) Press(AmigaTownBar town, CombatFieldMenuBar bar, Buttons button) =>
        town.HandleKey(bar, InputMap.MenuPadKey(button));

    [Fact]
    public void TownBar_StartPullsOptionsDown_DPadAndATakeAnItem()
    {
        var bar = TownBar();
        var town = new AmigaTownBar();
        Assert.True(town.HandleButton(bar, Buttons.Start));
        Assert.Equal(AmigaTownBar.OptionsMenu, town.Open);
        Assert.Equal(0, town.Item);
        Assert.Equal((true, ""), Press(town, bar, Buttons.DPadDown));
        Assert.Equal((true, ""), Press(town, bar, Buttons.DPadDown));
        Assert.Equal((true, "inn"), Press(town, bar, Buttons.A));
        Assert.Null(town.Open);
    }

    [Fact]
    public void TownBar_StartOrBClosesIt_AOpensIt_OtherButtonsAreNotTheBars()
    {
        var bar = TownBar();
        var town = new AmigaTownBar();
        Assert.Equal((true, ""), Press(town, bar, Buttons.A)); // Return pulls Options down
        Assert.Equal(AmigaTownBar.OptionsMenu, town.Open);
        Assert.True(town.HandleButton(bar, Buttons.Start));
        Assert.Null(town.Open);
        Press(town, bar, Buttons.DPadUp);
        Assert.Equal((true, ""), Press(town, bar, Buttons.B));
        Assert.Null(town.Open);
        Assert.Equal((false, ""), Press(town, bar, Buttons.B)); // closed: B goes on to the game menu
        Assert.Equal((false, ""), Press(town, bar, Buttons.LeftShoulder)); // Save goes on
        Assert.False(town.HandleButton(bar, Buttons.X));
        Assert.False(town.HandleButton(bar, Buttons.LeftTrigger)); // the shopper's
    }

    [Fact]
    public void OutsideBar_StartPullsOptionsDown_DPadAndATakeAnItem()
    {
        var outside = new AmigaOutsideBar();
        Assert.Equal((false, OutsideBarAct.None), outside.HandleButton(Buttons.A, AmigaCombatSpeed.Normal)); // closed: Fight
        Assert.Equal((false, OutsideBarAct.None), outside.HandleButton(Buttons.DPadDown, AmigaCombatSpeed.Normal)); // a step
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.Start, AmigaCombatSpeed.Normal));
        Assert.Equal(AmigaOutsideBar.OptionsMenu, outside.Open);
        Assert.Equal(0, outside.Item);
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.DPadUp, AmigaCombatSpeed.Normal)); // wraps
        Assert.Equal(3, outside.Item);
        Assert.Equal((true, OutsideBarAct.UseItem), outside.HandleButton(Buttons.A, AmigaCombatSpeed.Normal));
        Assert.Null(outside.Open);
    }

    [Fact]
    public void OutsideBar_RightGoesToSpeed_OnTheSpeedInForce_BCloses()
    {
        var outside = new AmigaOutsideBar();
        outside.HandleButton(Buttons.Start, AmigaCombatSpeed.Slow);
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.DPadRight, AmigaCombatSpeed.Slow));
        Assert.Equal(AmigaOutsideBar.SpeedMenu, outside.Open);
        Assert.Equal(2, outside.Item); // Slow
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.LeftThumbstickUp, AmigaCombatSpeed.Slow));
        Assert.Equal((true, OutsideBarAct.Normal), outside.HandleButton(Buttons.A, AmigaCombatSpeed.Slow));
        outside.HandleButton(Buttons.Start, AmigaCombatSpeed.Normal);
        outside.HandleButton(Buttons.DPadLeft, AmigaCombatSpeed.Normal);
        Assert.Equal(0, outside.Open); // File
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.B, AmigaCombatSpeed.Normal));
        Assert.Null(outside.Open);
        outside.HandleButton(Buttons.Start, AmigaCombatSpeed.Normal);
        Assert.Equal((false, OutsideBarAct.None), outside.HandleButton(Buttons.LeftShoulder, AmigaCombatSpeed.Normal)); // Save
        Assert.Equal((true, OutsideBarAct.None), outside.HandleButton(Buttons.Start, AmigaCombatSpeed.Normal));
        Assert.Null(outside.Open);
    }

    [Theory]
    [InlineData(Buttons.A)]
    [InlineData(Buttons.B)]
    public void OkWindow_ClosesWithAOrB(Buttons button)
    {
        var outside = new AmigaOutsideBar();
        outside.ShowCredits();
        Assert.True(outside.HandleWindowKeys(InputMap.MenuPadKeys([Buttons.DPadDown, Buttons.LeftShoulder])));
        Assert.True(outside.CreditsShown);
        Assert.True(outside.HandleWindowKeys(InputMap.MenuPadKeys([button])));
        Assert.False(outside.CreditsShown);
        outside.ShowStatus();
        outside.HandleWindowKeys(InputMap.MenuPadKeys([button]));
        Assert.False(outside.StatusShown);
    }

    /// <summary>I Amiga's map strip: Info, Options (Look ghosted outdoors), Speed.</summary>
    private static CombatFieldMenuBar MapBar() => new(10, 0, 13, 1,
        [Menu("about"), Menu("inspect", "health", "cast", "use_potion", "look"), Menu("fast", "normal", "slow")])
    {
        GhostedOutdoors = ["look"],
    };

    [Fact]
    public void MapBar_StartPullsOptionsDown_ATakesTheLitItem()
    {
        var bar = MapBar();
        var strip = new P1MapMenuBar();
        Assert.Equal((false, ""), strip.HandleButton(bar, Buttons.A, outdoors: true)); // closed: Fight
        Assert.Equal((true, ""), strip.HandleButton(bar, Buttons.Start, outdoors: true));
        Assert.Equal(P1MapMenuBar.OptionsMenu, strip.Open);
        Assert.Equal(0, strip.Item);
        strip.HandleButton(bar, Buttons.DPadDown, outdoors: true);
        strip.HandleButton(bar, Buttons.DPadDown, outdoors: true);
        Assert.Equal((true, "cast"), strip.HandleButton(bar, Buttons.A, outdoors: true));
        Assert.Null(strip.Open);
    }

    [Fact]
    public void MapBar_SkipsGhostedLookOutdoors_NotInADungeon()
    {
        var bar = MapBar();
        var strip = new P1MapMenuBar();
        strip.HandleButton(bar, Buttons.Start, outdoors: true);
        Assert.Equal((true, ""), strip.HandleButton(bar, Buttons.DPadUp, outdoors: true)); // wraps past Look
        Assert.Equal(3, strip.Item);
        strip.HandleButton(bar, Buttons.DPadDown, outdoors: true);
        Assert.Equal(0, strip.Item);
        strip.Close();
        strip.HandleButton(bar, Buttons.Start, outdoors: false);
        strip.HandleButton(bar, Buttons.LeftThumbstickUp, outdoors: false);
        Assert.Equal(4, strip.Item);
        Assert.Equal((true, "look"), strip.HandleButton(bar, Buttons.A, outdoors: false));
    }

    [Fact]
    public void MapBar_AOnAGhostedItemDoesNothing_LeftRightChangeMenu_BCloses()
    {
        var bar = MapBar();
        var strip = new P1MapMenuBar();
        strip.HandleButton(bar, Buttons.Start, outdoors: true);
        strip.Hover(bar, 4); // the mouse on Look
        Assert.Equal((true, ""), strip.HandleButton(bar, Buttons.A, outdoors: true));
        Assert.Equal(P1MapMenuBar.OptionsMenu, strip.Open);
        strip.HandleButton(bar, Buttons.DPadRight, outdoors: true);
        Assert.Equal(2, strip.Open);
        Assert.Equal(0, strip.Item);
        strip.HandleButton(bar, Buttons.DPadRight, outdoors: true);
        Assert.Equal(0, strip.Open); // wraps to Info
        Assert.Equal((true, "about"), strip.HandleButton(bar, Buttons.A, outdoors: true));
        strip.HandleButton(bar, Buttons.Start, outdoors: true);
        Assert.Equal((false, ""), strip.HandleButton(bar, Buttons.RightShoulder, outdoors: true)); // Load goes on
        Assert.Equal((true, ""), strip.HandleButton(bar, Buttons.B, outdoors: true));
        Assert.Null(strip.Open);
    }
}
