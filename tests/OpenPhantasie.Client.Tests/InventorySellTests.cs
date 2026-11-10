using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// QoL <c>modern-inventory</c> (user s178): the inventory screen sells in town. L (pad LT), the detail box's SELL line or a
/// drop on it asks "SELL X FOR N GP? (Y/N)"; Y / Enter (pad A) sells, anything else keeps it. Headless.
/// </summary>
public class InventorySellTests
{
    private const int LargeShield = 12, Club = 48;

    /// <summary>Fighto alone in Pelnor carrying only <paramref name="items"/>; the Manual preset (no Guild treasury, so a
    /// sale's gold stays in the purse) with the modern inventory on unless <paramref name="modern"/> is false.</summary>
    private static (GameSession S, InventoryScreen Screen, Character F) Town(bool modern = true, params int[] items)
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        var options = GameOptions.ForPreset(GameId.P1, OptionPreset.Manual);
        s.StartNewGame(modern ? options.With(RuleOptionRegistry.ModernInventory.Id, "on") : options);
        s.OpenGuild();
        var f = s.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(f, out _));
        s.EnterTown(1);
        f.Stats = new CharacterStats(18, 10, 18, 15, 10, 10);
        f.InventoryItemIds.Clear();
        f.InventoryItemIds.AddRange(items);
        f.WornShield = f.WornArmor = f.WornWeapon = 0;
        var screen = new InventoryScreen();
        screen.Open(s, ClientSessions.Data);
        return (s, screen, f);
    }

    [OriginalDataFact]
    public void L_AsksFirst_Y_Sells()
    {
        var (s, screen, _) = Town();
        s.Sack.ItemIds.Add(LargeShield);
        screen.HandleKey(Keys.Right); // the stash
        var price = s.SellPriceOf(LargeShield);
        Assert.True(price > 0);
        Assert.Equal(price, screen.SellPriceAt(screen.CursorSpot));
        var gold = s.Party.GoldCarried;

        var ask = screen.HandleKey(Keys.L);
        Assert.Contains("FOR " + price + " GP?", ask);
        Assert.NotNull(screen.Selling);
        Assert.Equal(UiText.S("inventory.help_sell"), screen.Help);
        Assert.Equal(1, s.Sack.ItemCount); // nothing sold yet

        screen.HandleKey(Keys.Y);
        Assert.Null(screen.Selling);
        Assert.Equal(0, s.Sack.ItemCount);
        Assert.Equal(gold + price, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void AnyOtherKey_KeepsIt()
    {
        var (s, screen, _) = Town();
        s.Sack.ItemIds.Add(LargeShield);
        screen.HandleKey(Keys.Right);
        screen.HandleKey(Keys.L);
        Assert.Equal(UiText.S("inventory.sell_cancelled"), screen.HandleKey(Keys.N));
        Assert.Equal(1, s.Sack.ItemCount);
        Assert.True(screen.IsOpen);

        screen.HandleKey(Keys.L);
        screen.HandleKey(Keys.Escape); // Esc answers no; it doesn't close the screen
        Assert.True(screen.IsOpen);
        Assert.Equal(1, s.Sack.ItemCount);
    }

    [OriginalDataFact]
    public void Pad_LT_Asks_A_Sells()
    {
        var (s, screen, f) = Town(true, Club);
        var row = screen.Layout.Slots.Count + f.InventoryItemIds.IndexOf(Club);
        for (var i = 0; i < row; i++)
            screen.HandleKey(Keys.Down);
        Assert.Equal(Club, screen.ItemAt(screen.CursorSpot));
        screen.HandleButton(Buttons.LeftTrigger);
        Assert.Equal(UiText.S("inventory.help_sell_pad"), screen.Help);
        screen.HandleButton(Buttons.A);
        Assert.DoesNotContain(Club, f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void DropOnTheSellLine_Asks()
    {
        var (s, screen, f) = Town(true, Club);
        var spot = new CarriedSpot(0, f.InventoryItemIds.IndexOf(Club));
        screen.PickUp(spot);
        Assert.Contains("GP?", screen.Drop(spot, new SellSpot()));
        Assert.Equal(spot, screen.Selling);
        Assert.Equal(UiText.F("inventory.done.sell", "CLUB", s.SellPriceOf(Club)), screen.HandleKey(Keys.Enter));
        Assert.DoesNotContain(Club, f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Original_NoSellLine_LRefuses()
    {
        var (s, screen, _) = Town(modern: false);
        s.Sack.ItemIds.Add(LargeShield);
        screen.HandleKey(Keys.Right);
        Assert.Null(screen.SellPriceAt(screen.CursorSpot));
        Assert.Null(screen.HandleKey(Keys.L)); // off: L does nothing, as before the option
        Assert.Null(screen.Selling);
        Assert.Equal(UiText.S("inventory.help"), screen.Help);
        Assert.Equal(1, s.Sack.ItemCount);
    }

    [OriginalDataFact]
    public void Modern_TownHelpShowsSell()
    {
        var (_, screen, _) = Town();
        Assert.True(screen.CanSellHere);
        Assert.Equal(UiText.S("inventory.help_town"), screen.Help);
    }

    [OriginalDataFact]
    public void SoldLine_SaysThePrice_EvenWhenTheTreasurySweepsIt()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster)); // modern inventory + Guild treasury
        s.OpenGuild();
        var f = s.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(f, out _));
        s.EnterTown(1);
        s.Sack.ItemIds.Add(LargeShield);
        var screen = new InventoryScreen();
        screen.Open(s, ClientSessions.Data);
        screen.HandleKey(Keys.Right);
        var price = s.SellPriceOf(LargeShield);
        screen.HandleKey(Keys.L);
        Assert.Equal(UiText.F("inventory.done.sell", screen.ItemName(LargeShield), price), screen.HandleKey(Keys.Y));
    }
}
