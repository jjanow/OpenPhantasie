using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// 09 T2 paperdoll + stash screen (<see cref="InventoryScreen"/>): the drop table, the keyboard twin of drag and drop,
/// read-only outside where the save's rules allow moves, and the refusal lines. Headless: no GraphicsDevice.
/// </summary>
public class InventoryScreenTests
{
    private const int SmallShield = 4, LargeShield = 12, Robes = 22, PlateMail = 30, Club = 48, Potion = 101;
    private static readonly CharacterStats Strong = new(18, 10, 18, 15, 10, 10);

    /// <summary>Fighto (member 0) + Wizo (member 1) in Pelnor, both strong enough for everything but plate mail.</summary>
    private static (GameSession S, InventoryScreen Screen, Character F, Character W) Town(OptionPreset preset = OptionPreset.Manual)
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1, preset));
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

    private static int SlotOf(Character ch, int id) => ch.InventoryItemIds.IndexOf(id);

    private static void FillTo9(Character ch)
    {
        while (CarriedItems.Count(ch) < Character.InventorySlots)
            CarriedItems.Put(ch, Potion);
    }

    // --- the drop table ---------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Plan_CarriedItem()
    {
        var (_, screen, f, _) = Town();
        CarriedItems.Put(f, LargeShield);
        var shield = new CarriedSpot(0, SlotOf(f, LargeShield));
        Assert.Equal(InventoryMoveKind.Equip, screen.Plan(shield, new SlotSpot(0, "shield")).Kind);
        Assert.Equal(InventoryMoveKind.WrongSlot, screen.Plan(shield, new SlotSpot(0, "weapon")).Kind);
        Assert.Equal(InventoryMoveKind.TransferAndEquip, screen.Plan(shield, new SlotSpot(1, "shield")).Kind);
        Assert.Equal(InventoryMoveKind.Transfer, screen.Plan(shield, new MemberTabSpot(1)).Kind);
        Assert.Equal(InventoryMoveKind.Transfer, screen.Plan(shield, new CarriedSpot(1, -1)).Kind);
        Assert.Equal(InventoryMoveKind.None, screen.Plan(shield, new CarriedSpot(0, 3)).Kind);
        Assert.Equal(InventoryMoveKind.None, screen.Plan(shield, new MemberTabSpot(0)).Kind);
        var toStash = screen.Plan(shield, new StashSpot(-1));
        Assert.Equal(new InventoryMove(InventoryMoveKind.ToStash, 0, shield.Slot), toStash);
    }

    [OriginalDataFact]
    public void Plan_WornBox_StandsForItsCarriedSlot()
    {
        var (_, screen, f, _) = Town();
        Assert.NotEqual(0, f.WornWeapon);
        var box = new SlotSpot(0, "weapon");
        var slot = SlotOf(f, f.WornWeapon);
        Assert.Equal(new InventoryMove(InventoryMoveKind.Unequip, 0, slot, 0, SlotId: "weapon"),
            screen.Plan(box, new CarriedSpot(0, -1)));
        Assert.Equal(new InventoryMove(InventoryMoveKind.ToStash, 0, slot), screen.Plan(box, new StashSpot(-1)));
        Assert.Equal(InventoryMoveKind.None, screen.Plan(box, box).Kind);
        Assert.Equal(new InventoryMove(InventoryMoveKind.Transfer, 0, slot, 1), screen.Plan(box, new MemberTabSpot(1)));
    }

    [OriginalDataFact]
    public void Plan_StashItem()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([0, LargeShield]);
        var item = new StashSpot(1);
        Assert.Equal(new InventoryMove(InventoryMoveKind.EquipFromStash, To: 1, StashSlot: 1, SlotId: "shield"),
            screen.Plan(item, new SlotSpot(1, "shield")));
        Assert.Equal(InventoryMoveKind.WrongSlot, screen.Plan(item, new SlotSpot(1, "armor")).Kind);
        Assert.Equal(new InventoryMove(InventoryMoveKind.FromStash, To: 1, StashSlot: 1), screen.Plan(item, new MemberTabSpot(1)));
        Assert.Equal(new InventoryMove(InventoryMoveKind.FromStash, To: 0, StashSlot: 1), screen.Plan(item, new CarriedSpot(0, 2)));
        Assert.Equal(InventoryMoveKind.None, screen.Plan(item, new StashSpot(-1)).Kind);
    }

    // --- doing it ---------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Drop_OnOwnBox_Wears_AndOnAnotherMembersBox_GivesAndWears()
    {
        var (_, screen, f, w) = Town();
        CarriedItems.Put(f, LargeShield);
        var name = screen.ItemName(LargeShield);
        Assert.Equal($"Fighto WEARS {name}.", screen.Drop(new CarriedSpot(0, SlotOf(f, LargeShield)), new SlotSpot(0, "shield")));
        Assert.Equal(LargeShield, f.WornShield);

        Assert.Equal($"{name} GIVEN TO Wizo, WHO WEARS IT.",
            screen.Drop(new SlotSpot(0, "shield"), new SlotSpot(1, "shield")));
        Assert.Equal(LargeShield, w.WornShield);
        Assert.DoesNotContain(LargeShield, f.InventoryItemIds);
        Assert.NotEqual(LargeShield, f.WornShield);
    }

    [OriginalDataFact]
    public void Drop_OnAWrongBox_IsRefused_AndNothingMoves()
    {
        var (_, screen, f, _) = Town();
        CarriedItems.Put(f, LargeShield);
        var before = f.InventoryItemIds.ToList();
        Assert.Equal("THAT GOES IN THE SHIELD BOX.",
            screen.Drop(new CarriedSpot(0, SlotOf(f, LargeShield)), new SlotSpot(0, "weapon")));
        Assert.Equal(before, f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void GiveAndWear_ToSomeoneWhoCantUseIt_IsRefused_BeforeAnythingMoves()
    {
        var (_, screen, f, w) = Town();
        CarriedItems.Put(f, PlateMail);
        f.Stats = new CharacterStats(20, 10, 18, 15, 10, 10);
        Assert.Equal("UNUSABLE", screen.Drop(new CarriedSpot(0, SlotOf(f, PlateMail)), new SlotSpot(1, "armor")));
        Assert.Contains(PlateMail, f.InventoryItemIds);
        Assert.DoesNotContain(PlateMail, w.InventoryItemIds);
    }

    [OriginalDataFact]
    public void Refusals_NameTheMember()
    {
        var (_, screen, f, w) = Town();
        FillTo9(w);
        Assert.Equal("Wizo CAN'T CARRY MORE.", screen.Drop(new CarriedSpot(0, SlotOf(f, f.WornWeapon)), new MemberTabSpot(1)));
        Assert.Equal("NO ITEM THERE.", screen.PickUp(new CarriedSpot(0, 8)));
        Assert.Null(screen.Held);
    }

    [OriginalDataFact]
    public void Keyboard_PickUp_MoveToStash_Place()
    {
        var (s, screen, f, _) = Town();
        var slots = screen.Layout.Slots.Count;
        var club = SlotOf(f, Club);
        Assert.True(club >= 0);
        for (var i = 0; i < slots + club; i++)
            screen.HandleKey(Keys.Down);
        Assert.Equal(new CarriedSpot(0, club), screen.CursorSpot);
        Assert.Equal("CLUB: WHERE TO?", screen.HandleKey(Keys.Enter));
        Assert.Equal(new CarriedSpot(0, club), screen.Held);
        screen.HandleKey(Keys.Right); // to the stash column
        Assert.True(screen.InStash);
        Assert.Equal("CLUB PUT IN THE SACK.", screen.HandleKey(Keys.Enter));
        Assert.Null(screen.Held);
        Assert.Equal([Club], s.Sack.ItemIds);
        Assert.Equal(0, f.WornWeapon); // its only club left, so it is no longer worn

        Assert.Equal("Fighto WEARS CLUB.", screen.HandleKey(Keys.E)); // E on the stash row: wear it from the stash
        Assert.Equal(Club, f.WornWeapon);
        Assert.Empty(s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void Keyboard_Shortcuts_StashTakeGive_EscPutsBackThenCloses()
    {
        var (s, screen, f, w) = Town();
        s.Sack.ItemIds.Add(Potion);
        screen.HandleKey(Keys.Right);
        Assert.Equal("Fighto TAKES " + screen.ItemName(Potion) + " FROM THE SACK.", screen.HandleKey(Keys.S));
        Assert.Contains(Potion, f.InventoryItemIds);

        screen.HandleKey(Keys.Left);
        var potion = SlotOf(f, Potion);
        while (screen.CursorSpot != new CarriedSpot(0, potion))
            screen.HandleKey(Keys.Down);
        Assert.StartsWith("GIVE ", screen.HandleKey(Keys.G));
        Assert.True(screen.Giving);
        Assert.Null(screen.HandleKey(Keys.Enter)); // G waits for a member
        Assert.EndsWith(" GIVEN TO Wizo.", screen.HandleKey(Keys.D2));
        Assert.Contains(Potion, w.InventoryItemIds);
        Assert.Equal(1, screen.Member);

        screen.HandleKey(Keys.Tab);
        Assert.Equal(0, screen.Member);
        screen.HandleKey(Keys.Up);
        screen.HandleKey(Keys.Enter); // pick up whatever is there
        Assert.NotNull(screen.Held);
        Assert.Equal("PUT BACK.", screen.HandleKey(Keys.Escape));
        Assert.True(screen.IsOpen);
        Assert.Equal("", screen.HandleKey(Keys.Escape));
        Assert.False(screen.IsOpen);
    }

    [OriginalDataFact]
    public void Keyboard_E_TakesOffAWornItem_U_SaysNothingWornOnACarriedOne()
    {
        var (_, screen, f, _) = Town();
        CarriedItems.Put(f, Potion);
        Assert.Equal(new SlotSpot(0, "shield"), screen.CursorSpot);
        Assert.StartsWith("Fighto TAKES OFF ", screen.HandleKey(Keys.E));
        Assert.Equal(0, f.WornShield);
        Assert.Equal("NOTHING WORN THERE.", screen.HandleKey(Keys.U));
        while (screen.CursorSpot != new CarriedSpot(0, SlotOf(f, Potion)))
            screen.HandleKey(Keys.Down);
        Assert.Equal("NOTHING WORN THERE.", screen.HandleKey(Keys.U));
        Assert.Equal("THAT CAN'T BE WORN.", screen.HandleKey(Keys.E));
    }

    [OriginalDataFact]
    public void ReadOnly_WhereTheRulesDontAllowMoves()
    {
        var s = ClientSessions.Overworld(); // manual rules: items move in town only
        var screen = new InventoryScreen();
        screen.Open(s, ClientSessions.Data);
        Assert.Equal("ITEMS CAN ONLY BE MOVED IN TOWN OR THE GUILD.", screen.ReadOnlyReason);
        var f = s.Party.Members[0];
        var before = f.InventoryItemIds.ToList();
        Assert.Equal("ITEMS CAN ONLY BE MOVED IN TOWN OR THE GUILD.", screen.PickUp(new CarriedSpot(0, SlotOf(f, f.WornWeapon))));
        Assert.Null(screen.Held);
        Assert.Equal("ITEMS CAN ONLY BE MOVED IN TOWN OR THE GUILD.", screen.HandleKey(Keys.S));
        Assert.Equal(before, f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void OriginalRules_TheGuildIsEditable()
    {
        // user, s75: "let me adjust inventory anywhere" — the Guild is not read-only under p1.stash-access = town.
        var (s, screen, f, w) = Town();
        s.OpenGuild();
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.Null(screen.ReadOnlyReason);
        Assert.Equal("CLUB: WHERE TO?", screen.PickUp(new CarriedSpot(0, SlotOf(f, Club))));
        Assert.Equal("CLUB GIVEN TO Wizo.", screen.Click(new MemberTabSpot(1)));
        Assert.Equal(2, w.InventoryItemIds.Count(id => id == Club));
    }

    [OriginalDataFact]
    public void OriginalRules_InAnAllowedPlace_ABlockSaysNotNow()
    {
        // An open D)ISTRIBUTE in town under the town rule: the place is fine, so "not now", not "only in town".
        var (s, screen, _, _) = Town();
        s.BeginItemDistribution();
        Assert.Equal("ITEMS CAN'T BE MOVED RIGHT NOW.", screen.ReadOnlyReason);
    }

    [OriginalDataFact]
    public void Anywhere_LetsTheWildernessMoveItems()
    {
        var (s, screen, _, _) = Town(OptionPreset.Remaster);
        s.DepartGuildToWilderness();
        Assert.NotEqual(GameMode.Town, s.Mode);
        Assert.Null(screen.ReadOnlyReason);
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        Assert.Equal("ITEMS CAN'T BE MOVED RIGHT NOW.", screen.ReadOnlyReason); // not "in a town": the rule allows anywhere
    }

    [OriginalDataFact]
    public void StashView_FiltersAndSorts_KeepingRawSlots()
    {
        var (s, screen, f, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, 0, Club, LargeShield, Robes]);
        Assert.Equal([(0, Potion), (2, Club), (3, LargeShield), (4, Robes)], screen.StashView());
        Assert.Equal("ALL", screen.FilterLabel);
        screen.HandleKey(Keys.F);
        Assert.Equal("SHIELD", screen.FilterLabel);
        Assert.Equal([(3, LargeShield)], screen.StashView());
        screen.HandleKey(Keys.F);
        screen.HandleKey(Keys.F);
        screen.HandleKey(Keys.F);
        Assert.Equal("OTHER", screen.FilterLabel);
        Assert.Equal([(0, Potion)], screen.StashView());
        screen.HandleKey(Keys.F); // back to all
        screen.HandleKey(Keys.O);
        Assert.Equal("NAME", screen.SortLabel);
        Assert.Equal(screen.StashView().Select(x => screen.ItemName(x.ItemId)).Order(StringComparer.Ordinal),
            screen.StashView().Select(x => screen.ItemName(x.ItemId)));
        screen.HandleKey(Keys.O);
        Assert.Equal("KIND", screen.SortLabel);
        Assert.Equal([3, 4, 2, 0], screen.StashView().Select(x => x.Slot)); // shield, armor, weapon, other

        // A move acts on the raw slot under the cursor.
        screen.HandleKey(Keys.Right);
        Assert.Equal(new StashSpot(3), screen.CursorSpot);
        screen.HandleKey(Keys.S);
        Assert.Contains(LargeShield, f.InventoryItemIds);
        Assert.Equal([Potion, 0, Club, 0, Robes], s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void StashScroll_StopsAtTheEnds()
    {
        var (s, screen, _, _) = Town();
        for (var i = 0; i < 10; i++)
            s.Sack.ItemIds.Add(Potion);
        screen.ScrollStash(-1);
        Assert.Equal(0, screen.StashScroll);
        for (var i = 0; i < 10; i++)
            screen.HandleKey(Keys.PageDown);
        Assert.Equal(10 - InventoryScreen.StashRows, screen.StashScroll);
        Assert.InRange(screen.StashRow, screen.StashScroll, screen.StashScroll + InventoryScreen.StashRows - 1);
    }

    [OriginalDataFact]
    public void Unusable_GreysOnlyGearTheMemberFails()
    {
        var (_, screen, f, _) = Town();
        Assert.Equal("UNUSABLE", screen.UnusableReason(f, PlateMail)); // needs STR 20 (manual p.21)
        Assert.Null(screen.UnusableReason(f, LargeShield));
        Assert.Null(screen.UnusableReason(f, Potion)); // not gear: not "unusable"
    }

    [OriginalDataFact]
    public void Pointing_MovesTheCursor_TabsSelectTheMember()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, SmallShield]);
        screen.Point(new StashSpot(1));
        Assert.Equal(new StashSpot(1), screen.CursorSpot);
        screen.Point(new SlotSpot(0, "armor"));
        Assert.Equal(new SlotSpot(0, "armor"), screen.CursorSpot);
        Assert.Null(screen.Click(new MemberTabSpot(1)));
        Assert.Equal(1, screen.Member);
        Assert.Null(screen.Click(new MemberTabSpot(5))); // no such member
        Assert.Equal(1, screen.Member);
    }

    [OriginalDataFact]
    public void ClickPickClickPlace_IsADrop()
    {
        var (s, screen, f, w) = Town();
        var club = new CarriedSpot(0, SlotOf(f, Club));
        Assert.Equal("CLUB: WHERE TO?", screen.Click(club));
        Assert.Equal("CLUB GIVEN TO Wizo.", screen.Click(new MemberTabSpot(1)));
        Assert.Equal(2, w.InventoryItemIds.Count(id => id == Club));
        Assert.Equal("", screen.Click(new StashFilterSpot()) ?? "");
        Assert.Equal("SHIELD", screen.FilterLabel);
        Assert.Empty(s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void Elemental_HasNoTab()
    {
        var (s, screen, _, _) = Town();
        var elemental = new Character { Name = "FIRE" };
        s.Party.Members.Add(elemental);
        s.Party.Elemental = elemental;
        Assert.Equal(2, screen.Members.Count);
    }

    [OriginalDataFact]
    public void NoParty_TheScreenStaysClosed_AndKeysDoNothing()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(5));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1)); // Pelnor with an empty party, as a fresh start
        var screen = new InventoryScreen();
        screen.Open(s, ClientSessions.Data);
        Assert.False(screen.IsOpen);
        Assert.Null(screen.HandleKey(Keys.Tab));
        Assert.Null(screen.HandleKey(Keys.Enter));
        Assert.Null(screen.Click(new MemberTabSpot(0)));
        Assert.Null(screen.PickUp(new CarriedSpot(0, 0)));
    }

    [OriginalDataFact]
    public void HelpLines_FitTheHelpStrip()
    {
        var (_, screen, f, _) = Town();
        // s99: the idle help names the detail box's [ / ] keys and takes two lines of the band (it wraps, never cut).
        Assert.True(screen.Help.Length <= 2 * 52, screen.Help);
        Assert.Contains("[ ]", screen.Help);
        screen.PickUp(new CarriedSpot(0, SlotOf(f, Club)));
        Assert.True(screen.Help.Length <= 52, screen.Help);
        screen.HandleKey(Keys.Escape);
        screen.HandleKey(Keys.G); // the cursor is still on the club
        Assert.True(screen.Giving);
        Assert.True(screen.Help.Length <= 52, screen.Help);
    }

    // --- the gamepad (user, s75) -----------------------------------------------------------------------------------

    [Fact]
    public void Pad_EveryScreenKeyHasAButton()
    {
        Keys[] screenKeys = [Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.PageUp, Keys.PageDown, Keys.Tab, Keys.Enter,
            Keys.Escape, Keys.E, Keys.U, Keys.S, Keys.G, Keys.F, Keys.O];
        var mapped = Enum.GetValues<Buttons>().Select(InventoryScreen.PadKey).OfType<Keys>().ToHashSet();
        Assert.All(screenKeys, k => Assert.Contains(k, mapped));
        // 1-6 (pick / give to a member) is LB / RB (+ A in the G step); the shell's pad X (V) closes.
        Assert.Null(InventoryScreen.PadKey(Buttons.LeftShoulder));
        Assert.Null(InventoryScreen.PadKey(Buttons.X));
    }

    [OriginalDataFact]
    public void Pad_DPadMoves_ShouldersPickTheMember()
    {
        var (s, screen, _, _) = Town();
        s.Sack.ItemIds.AddRange([Potion, SmallShield]);
        screen.HandleButton(Buttons.DPadDown);
        Assert.Equal(new SlotSpot(0, "armor"), screen.CursorSpot);
        screen.HandleButton(Buttons.DPadUp);
        Assert.Equal(new SlotSpot(0, "shield"), screen.CursorSpot);
        screen.HandleButton(Buttons.DPadRight);
        Assert.Equal(new StashSpot(0), screen.CursorSpot);
        screen.HandleButton(Buttons.LeftThumbstickDown);
        Assert.Equal(new StashSpot(1), screen.CursorSpot);
        screen.HandleButton(Buttons.DPadLeft);
        Assert.False(screen.InStash);
        screen.HandleButton(Buttons.RightShoulder);
        Assert.Equal(1, screen.Member);
        screen.HandleButton(Buttons.RightShoulder);
        Assert.Equal(0, screen.Member); // wraps
        screen.HandleButton(Buttons.LeftShoulder);
        Assert.Equal(1, screen.Member); // wraps back
        Assert.Null(screen.HandleButton(Buttons.X)); // the shell's close button: nothing here
        Assert.True(screen.IsOpen);
    }

    [OriginalDataFact]
    public void Pad_APicksAndPlaces_LikeEnter()
    {
        var (_, screen, f, w) = Town();
        while (screen.CursorSpot != new CarriedSpot(0, SlotOf(f, Club)))
            screen.HandleButton(Buttons.DPadDown);
        Assert.Equal("CLUB: WHERE TO?", screen.HandleButton(Buttons.A));
        screen.HandleButton(Buttons.RightShoulder); // Wizo's paperdoll, same row: his carried list
        Assert.Equal("CLUB GIVEN TO Wizo.", screen.HandleButton(Buttons.A));
        Assert.Equal(2, w.InventoryItemIds.Count(id => id == Club));
    }

    [OriginalDataFact]
    public void Pad_StartGives_ShouldersPickWho_AGives()
    {
        var (_, screen, f, w) = Town();
        while (screen.CursorSpot != new CarriedSpot(0, SlotOf(f, Club)))
            screen.HandleButton(Buttons.DPadDown);
        Assert.Equal("GIVE CLUB TO WHOM? (1-6)", screen.HandleButton(Buttons.Start));
        Assert.True(screen.Giving);
        Assert.Equal(UiText.S("inventory.help_give_pad"), screen.Help);
        screen.HandleButton(Buttons.RightShoulder);
        Assert.True(screen.Giving);
        Assert.Equal("CLUB GIVEN TO Wizo.", screen.HandleButton(Buttons.A));
        Assert.Equal(2, w.InventoryItemIds.Count(id => id == Club));
        Assert.False(screen.Giving);
    }

    [OriginalDataFact]
    public void Pad_Y_Wears_Back_TakesOff_RT_Sack()
    {
        var (s, screen, f, _) = Town();
        Assert.Equal(new SlotSpot(0, "shield"), screen.CursorSpot);
        Assert.StartsWith("Fighto TAKES OFF ", screen.HandleButton(Buttons.Y));
        Assert.Equal(0, f.WornShield);
        Assert.Equal("NOTHING WORN THERE.", screen.HandleButton(Buttons.Back));
        while (screen.CursorSpot != new CarriedSpot(0, SlotOf(f, SmallShield)))
            screen.HandleButton(Buttons.DPadDown);
        Assert.StartsWith("Fighto WEARS ", screen.HandleButton(Buttons.Y));
        Assert.Equal(SmallShield, f.WornShield);
        Assert.StartsWith("Fighto TAKES OFF ", screen.HandleButton(Buttons.Back));
        Assert.EndsWith(" PUT IN THE SACK.", screen.HandleButton(Buttons.RightTrigger));
        Assert.Contains(SmallShield, s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void Pad_StickClicksFilterAndSort_RightStickScrolls()
    {
        var (s, screen, _, _) = Town();
        for (var i = 0; i < 10; i++)
            s.Sack.ItemIds.Add(Potion);
        screen.HandleButton(Buttons.LeftStick);
        Assert.Equal("SHIELD", screen.FilterLabel);
        screen.HandleButton(Buttons.RightStick);
        Assert.Equal("NAME", screen.SortLabel);
        for (var i = 0; i < 4; i++)
            screen.HandleButton(Buttons.LeftStick);
        Assert.Equal("ALL", screen.FilterLabel);
        screen.HandleButton(Buttons.RightThumbstickDown);
        Assert.Equal(1, screen.StashScroll);
        screen.HandleButton(Buttons.RightThumbstickUp);
        Assert.Equal(0, screen.StashScroll);
    }

    [OriginalDataFact]
    public void Pad_BPutsBack_ThenCloses()
    {
        var (_, screen, f, _) = Town();
        screen.PickUp(new CarriedSpot(0, SlotOf(f, Club)));
        Assert.Equal("PUT BACK.", screen.HandleButton(Buttons.B));
        Assert.Null(screen.Held);
        Assert.True(screen.IsOpen);
        screen.HandleButton(Buttons.B);
        Assert.False(screen.IsOpen);
        Assert.Null(screen.HandleButton(Buttons.A)); // closed: nothing
    }

    [OriginalDataFact]
    public void Pad_HelpShowsButtonHints_UntilAKeyIsPressed()
    {
        var (_, screen, f, _) = Town();
        Assert.Equal(UiText.S("inventory.help"), screen.Help);
        screen.HandleButton(Buttons.DPadDown);
        Assert.Equal(UiText.S("inventory.help_pad"), screen.Help);
        Assert.True(screen.Help.Length <= 2 * 52, screen.Help); // s183: + LB RB: wraps to a second line, never cut
        Assert.Contains("LB RB", screen.Help);
        screen.PickUp(new CarriedSpot(0, SlotOf(f, Club)));
        Assert.Equal(UiText.S("inventory.help_held_pad"), screen.Help);
        Assert.True(screen.Help.Length <= 52, screen.Help);
        screen.HandleKey(Keys.Escape);
        Assert.Equal(UiText.S("inventory.help"), screen.Help);
    }

    [Fact]
    public void InputMap_NewButtons_AreEdgeTriggered()
    {
        var input = new InputMap(new ClientSettings());
        var a = new GamePadState(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(Buttons.A), new GamePadDPad());
        Assert.Equal([Buttons.A], input.NewButtons(a));
        input.EndFrame(new KeyboardState(), a);
        Assert.Empty(input.NewButtons(a)); // still held: not new
        var dpad = new GamePadState(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(Buttons.A),
            new GamePadDPad(ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released));
        Assert.Equal([Buttons.DPadDown], input.NewButtons(dpad));
    }

    [Fact]
    public void Wrap_BreaksAtWords()
    {
        Assert.Equal(["THE ARMORY", "PAYS 12 GP"], UiOverlays.Wrap("THE ARMORY PAYS 12 GP", 10));
        Assert.Equal(["ABCDE", "FGH"], UiOverlays.Wrap("ABCDEFGH", 5)); // s99: a long word goes on, never cut
        // s156: a too-wide word breaks after its last '/' (or '-') that fits, not inside a part.
        Assert.Equal(["NUMPAD/", "ARROWS MOVE"], UiOverlays.Wrap("NUMPAD/ARROWS MOVE", 11));
        Assert.Equal(["UP-", "DOWN"], UiOverlays.Wrap("UP-DOWN", 5));
    }

    [Fact]
    public void Layout_FitsTheContentArea()
    {
        var slots = EquipmentLayout.PhantasieI.Slots.Count;
        var panel = UiOverlays.InventoryLayout.Panel;
        Assert.True(UiOverlays.InventoryLayout.CarriedRow(slots, Character.InventorySlots - 1).Bottom <= panel.Bottom - 1);
        Assert.True(UiOverlays.InventoryLayout.Tab(5).Right <= panel.Right - 1);
        Assert.True(UiOverlays.InventoryLayout.StashRow(InventoryScreen.StashRows - 1).Bottom <= UiOverlays.InventoryLayout.DetailTop - 3);
        Assert.True(UiOverlays.InventoryLayout.DetailTop + UiOverlays.InventoryLayout.DetailLines * 8 <= panel.Bottom);
        Assert.True(panel.Bottom <= UiOverlays.ModalArea.Bottom);
    }

    [Theory]
    [InlineData(3, 32, 0)] // I, II: everything where it was
    [InlineData(4, 48, 1)] // III Amiga: four worn boxes beside the whole 32×48 portrait; the gear line goes right (s99)
    [InlineData(5, 32, 2)] // IV: five worn boxes; the name line goes right too, the boxes start at the portrait's top
    public void Layout_PortraitAndCarriedRowsFitThePanel(int slots, int portraitHeight, int moved)
    {
        var panel = UiOverlays.InventoryLayout.Panel;
        var a = UiOverlays.InventoryLayout.Arrange(slots, portraitHeight);
        Assert.Equal(moved, a.Moved);
        Assert.True(a.CarriedHeaderY >= UiOverlays.InventoryLayout.PortraitTop + portraitHeight + 2);
        Assert.True(a.Slot(slots - 1).Bottom <= a.CarriedHeaderY - 1);
        var last = a.CarriedRow(Character.InventorySlots - 1);
        Assert.True(last.Bottom <= panel.Bottom - 1, $"{last} past {panel}");
        Assert.Equal(8, last.Height); // the font's own row spacing, never closed up
        // The lines moved right sit above the sack, whose rows end above the detail box; the sack keeps at least 4 rows.
        Assert.True(a.StashRow(a.StashRowsShown - 1).Bottom <= UiOverlays.InventoryLayout.DetailTop - 3);
        Assert.InRange(a.StashRowsShown, 4, InventoryScreen.StashRows);
        Assert.Equal(UiOverlays.InventoryLayout.StashTop + moved * PixelFont.LineHeight, a.StashTop);
        if (moved == 0) // I's rows as before: 8 apart from y 88, the sack's seven rows from y 40
        {
            Assert.Equal(new Microsoft.Xna.Framework.Rectangle(6, 88, 152, 8), a.CarriedRow(0));
            Assert.Equal(InventoryScreen.StashRows, a.StashRowsShown);
        }
    }

    [Fact]
    public void Tabs_KeepTheFixedRowWhileNamesFit_ElseGrowToTheName_s99()
    {
        string[] fits = ["1SWAT", "2FRANCIS", "3LEFTY"];
        Assert.Equal(fits.Select((_, i) => UiOverlays.InventoryLayout.Tab(i)), UiOverlays.InventoryLayout.Tabs(fits, 0));
        string[] longer = ["1SWAT", "2ARENTINO", "3LEFTY", "4FRANCIS", "5FOLNAR", "6ZANDOR"];
        var tabs = UiOverlays.InventoryLayout.Tabs(longer, 1);
        for (var i = 0; i < longer.Length; i++)
            Assert.True(tabs[i].Width >= PixelFont.Measure(longer[i]) + 4, longer[i]);
        Assert.True(tabs[^1].Right <= UiOverlays.InventoryLayout.Panel.Right - 2);
        // Six nine-letter names do not fit the row: it slides so the current tab shows whole.
        string[] widest = [.. Enumerable.Range(1, 6).Select(i => $"{i}ABCDEFGHI")];
        var slid = UiOverlays.InventoryLayout.Tabs(widest, 5);
        Assert.True(slid[5].Right <= UiOverlays.InventoryLayout.Panel.Right - 2 && slid[5].X >= UiOverlays.InventoryLayout.TabsLeft);
    }

    [Fact]
    public void InventoryScreenSetting_IsOff_ByDefault_AndRoundTrips()
    {
        Assert.False(new ClientSettings().Game(GameId.P1).InventoryScreen); // off = the original V overlay
        var on = ClientSettings.FromJson("""{ "games": { "p1": { "inventoryScreen": true } } }""")!;
        Assert.True(on.Game(GameId.P1).InventoryScreen);
        Assert.False(on.Game(GameId.P2).InventoryScreen);
    }
}
