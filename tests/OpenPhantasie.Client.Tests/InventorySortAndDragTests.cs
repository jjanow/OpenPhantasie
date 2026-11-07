using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// PLAN P3.2 leftovers on the paperdoll + stash screen (<see cref="InventoryScreen"/>, behind
/// <see cref="GameSettings.InventoryScreen"/>): the stash sorts by value too, and a mouse drag over a member tab shows that
/// member, so a stash item or another member's item can be dropped on their paperdoll boxes.
/// </summary>
public class InventorySortAndDragTests
{
    private const int LargeShield = 12, Robes = 22, Club = 48, Potion = 101;
    private static readonly CharacterStats Strong = new(18, 10, 18, 15, 10, 10);

    private static (GameSession S, InventoryScreen Screen, Character F, Character W) Town()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1));
        s.OpenGuild();
        var f = s.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        var w = s.CreateAndEnroll("Wizo", Race.Human, CharacterClass.Wizard);
        Assert.True(s.TryAddToParty(f, out _));
        Assert.True(s.TryAddToParty(w, out _));
        s.EnterTown(1);
        f.Stats = Strong;
        w.Stats = Strong;
        var screen = new InventoryScreen();
        screen.Open(s, ClientSessions.Data);
        return (s, screen, f, w);
    }

    [OriginalDataFact]
    public void Sort_CyclesFoundNameKindValue_ValueIsTheArmoryPriceHighestFirst()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, 0, Club, LargeShield, Robes]);
        Assert.Equal("FOUND", screen.SortLabel);
        foreach (var label in new[] { "NAME", "KIND", "VALUE", "FOUND" })
        {
            screen.HandleKey(Keys.O);
            Assert.Equal(label, screen.SortLabel);
        }
        screen.HandleKey(Keys.O);
        screen.HandleKey(Keys.O);
        screen.HandleKey(Keys.O);
        Assert.Equal(InventoryScreen.StashSort.Value, screen.Sort);

        var view = screen.StashView();
        var prices = view.Select(x => screen.ItemValue(x.ItemId)).ToList();
        Assert.Equal(prices.OrderByDescending(p => p), prices);
        Assert.Equal([0, 2, 3, 4], view.Select(x => x.Slot).Order()); // raw slots kept, holes left out
        foreach (var (_, id) in view)
        {
            var item = ClientSessions.Data.Items.First(i => i.Id == id);
            Assert.Equal(ItemRules.SellPrice(item.ValueFactor), screen.ItemValue(id));
        }
        Assert.Contains(prices, p => p > 0);
    }

    [OriginalDataFact]
    public void Sort_Value_EqualPricesKeepTheFoundOrder()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, Club, Potion]);
        for (var k = 0; k < 3; k++)
            screen.HandleKey(Keys.O);
        var view = screen.StashView();
        var potions = view.Where(x => x.ItemId == Potion).Select(x => x.Slot).ToList();
        Assert.Equal([0, 2], potions);
    }

    [OriginalDataFact]
    public void DragOver_ATab_WithAnItemHeld_ShowsThatMember()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.Add(LargeShield);
        Assert.Equal(0, screen.Member);

        screen.DragOver(new MemberTabSpot(1)); // nothing held: a tab is not a place to point at
        Assert.Equal(0, screen.Member);

        screen.PickUp(new StashSpot(0));
        screen.DragOver(new MemberTabSpot(1));
        Assert.Equal(1, screen.Member);
        screen.DragOver(new MemberTabSpot(7)); // no such member
        Assert.Equal(1, screen.Member);
    }

    [OriginalDataFact]
    public void Drag_StashItem_OverATab_OntoThatMembersBox_WearsItThere()
    {
        var (s, screen, f, w) = Town();
        s.Sack.ItemIds.Add(LargeShield);
        Assert.NotEqual(LargeShield, w.WornShield);

        screen.PickUp(new StashSpot(0));          // drag starts on the stash row
        screen.DragOver(new MemberTabSpot(1));    // over Wizo's tab
        var line = screen.Drop(new StashSpot(0), new SlotSpot(screen.Member, "shield")); // let go on the shown box
        Assert.Equal(LargeShield, w.WornShield);
        Assert.NotEqual(LargeShield, f.WornShield);
        Assert.DoesNotContain(s.Sack.ItemIds, id => id != 0);
        Assert.Contains("WIZO", line.ToUpperInvariant());
    }

    [OriginalDataFact]
    public void Drag_CarriedItem_OverAnotherTab_OntoTheirBox_GivesAndWears()
    {
        var (_, screen, f, w) = Town();
        CarriedItems.Put(f, LargeShield);
        var from = new CarriedSpot(0, f.InventoryItemIds.IndexOf(LargeShield));

        screen.PickUp(from);
        screen.DragOver(new MemberTabSpot(1));
        Assert.Equal(InventoryMoveKind.TransferAndEquip, screen.Plan(from, new SlotSpot(screen.Member, "shield")).Kind);
        screen.Drop(from, new SlotSpot(screen.Member, "shield"));
        Assert.DoesNotContain(LargeShield, f.InventoryItemIds);
        Assert.Equal(LargeShield, w.WornShield);
    }

    [OriginalDataFact]
    public void DragOver_ARow_MovesTheCursorAsHoverDoes()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, Club]);
        screen.DragOver(new StashSpot(1));
        Assert.True(screen.InStash);
        Assert.Equal(new StashSpot(1), screen.CursorSpot);
    }
}
