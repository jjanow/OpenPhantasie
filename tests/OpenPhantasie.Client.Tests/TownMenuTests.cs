using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client.Tests;

/// <summary>The DOS town menu bar: order, highlight, Armory as a place, Bank / Option lists.</summary>
public class TownMenuTests
{
    private static TownMenu At(string label)
    {
        var menu = new TownMenu();
        while (TownMenu.BarLabels[menu.BarCursor] != label)
            menu.HandleKey(Keys.Right);
        return menu;
    }

    [Fact]
    public void Bar_IsTheDosLabelTable()
    {
        Assert.Equal(["Option", "Bank", "Mystic", "Exit", "Armory", "Inn", "Guild"], TownMenu.BarLabels);
        var menu = new TownMenu();
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [Fact]
    public void Arrows_WrapAroundTheBar_FirstEverOnGuild()
    {
        var menu = new TownMenu();
        Assert.Equal("Guild", TownMenu.BarLabels[menu.BarCursor]); // menu 0x1's initial selection word
        menu.HandleKey(Keys.Right);
        Assert.Equal("Option", TownMenu.BarLabels[menu.BarCursor]);
        menu.HandleKey(Keys.Left);
        Assert.Equal("Guild", TownMenu.BarLabels[menu.BarCursor]);
    }

    [Fact]
    public void Armory_IsAPlace_AndLeavingReturnsToTheBar()
    {
        var menu = At("Armory");
        Assert.Null(menu.HandleKey(Keys.Enter));
        Assert.Equal(TownPlace.Armory, menu.Place);
        Assert.Null(menu.HandleKey(Keys.Left)); // the Armory owns its keys
        Assert.Equal("Armory", TownMenu.BarLabels[menu.BarCursor]);
        menu.LeaveArmory();
        Assert.Equal(TownPlace.Menu, menu.Place);
        Assert.Equal("Armory", TownMenu.BarLabels[menu.BarCursor]); // the highlight is remembered
    }

    [Theory]
    [InlineData("Mystic", TownChoice.Mystic)]
    [InlineData("Exit", TownChoice.Exit)]
    [InlineData("Inn", TownChoice.Inn)]
    [InlineData("Guild", TownChoice.Guild)]
    public void DirectEntries_ReturnTheirAction(string label, TownChoice expected)
    {
        var menu = At(label);
        Assert.Equal(expected, menu.HandleKey(Keys.Space));
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [OriginalDataFact]
    public void Bank_ListsWithdrawDeposit_AndExitClosesIt()
    {
        var menu = At("Bank");
        menu.HandleKey(Keys.Enter);
        Assert.Equal(TownPlace.Bank, menu.Place);
        Assert.Equal(["Withdraw", "Deposit", "Exit"], menu.SubLabels);
        menu.HandleKey(Keys.Down);
        Assert.Equal(TownChoice.Deposit, menu.HandleKey(Keys.Enter));
        Assert.Equal(TownPlace.Menu, menu.Place);

        menu.HandleKey(Keys.Enter); // reopens on its last choice (s58: per-menu cursors)
        Assert.Equal(1, menu.SubCursor);
        menu.HandleKey(Keys.Down);
        Assert.Null(menu.HandleKey(Keys.Enter));
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [Fact]
    public void Option_Backspace_ReturnsToTheBar()
    {
        var menu = At("Option");
        menu.HandleKey(Keys.Enter);
        Assert.Equal(TownPlace.Option, menu.Place);
        menu.HandleKey(Keys.Back);
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [Fact]
    public void Back_LeavesArmoryOrList_ForTheBar_FalseOnTheBar()
    {
        var menu = At("Armory");
        menu.HandleKey(Keys.Enter);
        Assert.Equal(TownPlace.Armory, menu.Place);
        Assert.True(menu.Back());
        Assert.Equal(TownPlace.Menu, menu.Place);
        Assert.False(menu.Back()); // the bar has no parent: the shell opens the game menu
    }

    [OriginalDataFact]
    public void Option_ReopensOnItsLastChoice_BankKeepsItsOwn_s58()
    {
        var menu = At("Option");
        menu.HandleKey(Keys.Enter);
        menu.HandleKey(Keys.Up);
        Assert.Equal(TownChoice.ShowParty, menu.HandleKey(Keys.Enter));
        menu = AtSame(menu, "Bank");
        menu.HandleKey(Keys.Enter);
        Assert.Equal(0, menu.SubCursor); // Bank's own cursor, first entry
        menu.HandleKey(Keys.Back);
        menu = AtSame(menu, "Option");
        menu.HandleKey(Keys.Enter);
        Assert.Equal("Show party", menu.SubLabels[menu.SubCursor]);
    }

    private static TownMenu AtSame(TownMenu menu, string label)
    {
        while (TownMenu.BarLabels[menu.BarCursor] != label)
            menu.HandleKey(Keys.Right);
        return menu;
    }

    [Fact]
    public void Reset_LeavesAnyPlace()
    {
        var menu = At("Armory");
        menu.HandleKey(Keys.Enter);
        menu.Reset();
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [Theory] // D-066 (user s53): the manual's letters work on the bar too
    [InlineData(Keys.M, TownChoice.Mystic)]
    [InlineData(Keys.N, TownChoice.Inn)]
    [InlineData(Keys.G, TownChoice.Guild)]
    [InlineData(Keys.L, TownChoice.Exit)]
    [InlineData(Keys.U, TownChoice.UseItem)]
    [InlineData(Keys.I, TownChoice.ShowParty)]
    [InlineData(Keys.D, TownChoice.Distribute)]
    [InlineData(Keys.S, TownChoice.SaveGame)]
    public void ManualLetters_PickFromTheBar(Keys key, TownChoice expected) =>
        Assert.Equal(expected, new TownMenu().HandleKey(key));

    [Fact] // III's manual: T Trade Items (III PDF 13 / pr.21); only where the rules have Trade
    public void ManualLetterT_IsTrade_OnlyWithTrade()
    {
        Assert.Equal(TownChoice.Trade, new TownMenu { Trade = true }.HandleKey(Keys.T));
        Assert.Null(new TownMenu().HandleKey(Keys.T));
    }

    [Fact]
    public void ManualLetters_AandB_OpenArmoryAndBank()
    {
        var menu = new TownMenu();
        Assert.Null(menu.HandleKey(Keys.A));
        Assert.Equal(TownPlace.Armory, menu.Place);
        menu.LeaveArmory();
        Assert.Null(menu.HandleKey(Keys.B));
        Assert.Equal(TownPlace.Bank, menu.Place);
        Assert.Null(menu.HandleKey(Keys.M)); // letters belong to the bar only
    }

    [OriginalDataFact]
    public void Option_IsTheDosMenu3_WithTogglePrefixes()
    {
        var menu = At("Option");
        (menu.AutoSave, menu.Sound) = (false, true);
        menu.HandleKey(Keys.Enter);
        Assert.Equal(["Cast spell", "Use item", "Show party", "Save game", "-Auto save", "+Sound", "Printout", "Exit"],
            menu.SubLabels);
        menu.AutoSave = true;
        Assert.Equal("+Auto save", menu.SubLabels[4]);
    }

    [Theory]
    [InlineData(3, TownChoice.SaveGame)]
    [InlineData(4, TownChoice.ToggleAutoSave)]
    [InlineData(5, TownChoice.ToggleSound)]
    [InlineData(6, TownChoice.Printout)]
    public void Option_PicksTheNewEntries(int index, TownChoice expected)
    {
        var menu = At("Option");
        menu.HandleKey(Keys.Enter);
        Assert.Equal(TownMenu.FirstOptionCursor, menu.SubCursor); // first-ever: Save game (menu 0x3)
        for (var i = TownMenu.FirstOptionCursor; i < index; i++)
            menu.HandleKey(Keys.Down);
        Assert.Equal(expected, menu.HandleKey(Keys.Enter));
        Assert.Equal(TownPlace.Menu, menu.Place);
    }

    [OriginalDataFact]
    public void InnMenu_0x0d_ExitInn_Distribute_Printout()
    {
        var menu = new TownMenu();
        menu.OpenInn();
        Assert.Equal(TownPlace.Inn, menu.Place);
        Assert.Equal(["Exit Inn", "Distribute", "Printout"], menu.SubLabels);
        Assert.Null(menu.HandleKey(Keys.Enter)); // Exit Inn: back to the bar
        Assert.Equal(TownPlace.Menu, menu.Place);

        menu.OpenInn();
        menu.HandleKey(Keys.Down);
        Assert.Equal(TownChoice.Distribute, menu.HandleKey(Keys.Enter));
        menu.OpenInn(); // reopens on Distribute
        menu.HandleKey(Keys.Down);
        Assert.Equal(TownChoice.Printout, menu.HandleKey(Keys.Enter));
    }
}
