using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s192: the pad fix list's C 14-23 (<c>docs/plan/14-pad-fix-list.md</c>): the inventory's detail scroll, the town's pad
/// command list (Distribute without a rest), III Amiga Guild Info, I Amiga's FIGHT pick, the requesters' outlined picks,
/// A on the DOS digit steps, IV's title menu, the scale chord, and the small ones (slot picker, Treasury, II ST clicks).
/// </summary>
public class PadFixListC2Tests
{
    private static GamePadState PadDown(params Buttons[] buttons) =>
        new(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(buttons.Aggregate((Buttons)0, (a, b) => a | b)),
            new GamePadDPad());

    // --- C14 -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void C14_RightStickSideways_ScrollsTheDetailBox_AndTheHelpNamesIt()
    {
        Assert.Equal(Keys.OemOpenBrackets, InventoryScreen.PadKey(Buttons.RightThumbstickLeft));
        Assert.Equal(Keys.OemCloseBrackets, InventoryScreen.PadKey(Buttons.RightThumbstickRight));
        Assert.Contains("RS scroll", UiText.S("inventory.help_pad"));
        Assert.Contains("RS scroll", UiText.S("inventory.help_pad_town"));
    }

    // --- C15 / C16 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void C16_TheGuildListsInfo_OnlyWhereTheStreetBarHasIt()
    {
        var settings = new ClientSettings();
        Assert.DoesNotContain(PadCommandDialog.EntriesFor(GameMode.Guild, settings.GetKey, true, false),
            e => e.Key == Keys.OemQuestion);
        var guild = PadCommandDialog.EntriesFor(GameMode.Guild, settings.GetKey, true, false, info: true);
        Assert.Equal(UiText.S("pad.cmd.info"), guild.Single(e => e.Key == Keys.OemQuestion).Label);
        Assert.Equal(Keys.E, guild[^1].Key); // Exit Guild stays last
        Assert.Equal(OutsideBarAct.Info, AmigaOutsideBar.ForKey(Keys.OemQuestion));
    }

    [Fact]
    public void C15_TheTownHelp_NamesBack()
    {
        Assert.Contains("Back", UiText.S("game.arrows_enter_a_b_m_n.pad"));
        Assert.Equal(UiText.S("pad.cmd.distribute"),
            PadCommandDialog.EntriesFor(GameMode.Town, new ClientSettings().GetKey, false, false)[0].Label);
    }

    // --- C17 ------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void C17_IAmigaOrdersList_EndsOnFight_WhichAPicks()
    {
        var s = ClientSessions.Fight();
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = layout, AmigaP1Field = () => true };
        ui.Start(s);
        var pad = new CombatPad();
        foreach (var key in pad.KeysFor(ui.PadStep(s), [Buttons.A])) // Party Options down, on Fight
            ui.HandleKey(s, key);
        foreach (var key in pad.KeysFor(ui.PadStep(s), [Buttons.A])) // Fight
            ui.HandleKey(s, key);
        Assert.True(ui.P1OrdersShown);
        var step = ui.PadStep(s)!;
        Assert.Equal(UiText.S("p1field.item.fight"), step.Picks[^1].Label);
        Assert.Equal([Keys.F], step.Picks[^1].Keys);
        var keys = pad.KeysFor(step, [Buttons.DPadUp, Buttons.A]); // Up from nothing lands on the last pick: FIGHT
        Assert.Equal([Keys.F], keys);
        foreach (var key in keys)
            ui.HandleKey(s, key);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
    }

    // --- C18 ------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void C18_IAmigaAddMoveQuestion_IsAYesNoStep_ForThePad()
    {
        var add = new GuildAddDialog(new PromptMenu(), ClientSessions.Data);
        Assert.False(add.AsksYesNo); // nothing asked
        add.P1Requester = true;
        Assert.False(add.AsksYesNo); // still nothing asked: the list keeps its own keys
        Assert.False(add.TakesLeftRight);
        Assert.Equal("A yes  X no  B cancel", UiText.Table.Get("guild.add_move_y_n.pad"));
        Assert.Equal([Keys.Y, Keys.N], DialogPad.YesNoKeys([Buttons.A, Buttons.X]));
    }

    [OriginalDataFact]
    public void C18_TheEventPagePick_HasAShortParchmentForm()
    {
        Assert.Equal("> 2) GO", UiText.F("pad.event_page_pick_x_x", 2, "GO"));
        Assert.Equal(1, InspectDialog.PageMenu.Options.ToList().IndexOf(UiText.S("inspect.equipment"))); // button 1 = digit 2
    }

    // --- C19 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C19_AWithNoPick_TakesTheFirstPick_OnADigitStep_ElseItsAKey()
    {
        var pad = new CombatPad();
        CombatPadStep Step(string id, bool first) =>
            new(id, [new CombatPadPick("1", [Keys.D1]), new CombatPadPick("2", [Keys.D2])], AFirstPick: first);
        Assert.Equal([Keys.D1], pad.KeysFor(Step("rank", true), [Buttons.A]));
        Assert.Equal([Keys.Enter], pad.KeysFor(Step("menu", false), [Buttons.A])); // the order menu: A keeps the order
        Assert.Equal([Keys.D2], pad.KeysFor(Step("rank2", true), [Buttons.DPadDown, Buttons.DPadDown, Buttons.A]));
    }

    // --- C20 / C21 ------------------------------------------------------------------------------------------------------

    private static TitleSplash DiskMenu(Func<int, bool> saved)
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;
        var splash = new TitleSplash(GameId.P4, pack.TitleSequence!) { Panels = pack.CommandPanels };
        splash.MenuIcons = TitleSplash.DiskMenuIcons(pack.CommandPanels!.DiskMenu!, saved);
        splash.Update(0.016, false, true); // Esc / B: to the menu
        Assert.NotNull(splash.CurrentMenu);
        return splash;
    }

    [OriginalDataFact]
    public void C20_AOnArrival_PicksNothing_ThenTheLitIcon()
    {
        var splash = DiskMenu(k => k is 1 or 3);
        Assert.False(splash.PadChoose()); // A mashed through the pictures: the menu has just shown
        splash.Update(TitleSplash.PadSettleSeconds, false, false);
        Assert.True(splash.MenuSettled);
        splash.PadStep(1); // the first press lights New Game
        splash.PadStep(1);
        splash.PadStep(1); // slot 3
        Assert.Equal(2, splash.PadCursor);
        splash.PadStep(1); // wraps
        Assert.Equal(0, splash.PadCursor);
        splash.PadStep(-1);
        Assert.True(splash.PadChoose());
        Assert.Equal("3", splash.MenuPick);
    }

    [OriginalDataFact]
    public void C20_AWithNothingLit_IsNewGame_AndTheHelpHasAPadTwin()
    {
        var splash = DiskMenu(_ => false);
        splash.Update(1, false, false);
        Assert.True(splash.PadChoose());
        Assert.Equal("N", splash.MenuPick);
        Assert.Contains("D-pad", UiText.S("title.disk_help.pad"));
    }

    // --- C22 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C22_StartHeldThenBack_CyclesTheScale_NotTheAutoMap()
    {
        var input = new InputMap(new ClientSettings());
        input.EndFrame(new KeyboardState(), PadDown(Buttons.Start));
        var chord = PadDown(Buttons.Start, Buttons.Back);
        Assert.True(input.Pressed(GameAction.CycleScale, new KeyboardState(), chord));
        Assert.False(input.Pressed(GameAction.ToggleAutoMap, new KeyboardState(), chord));
        var plain = new InputMap(new ClientSettings());
        plain.EndFrame(new KeyboardState(), default);
        Assert.True(plain.Pressed(GameAction.ToggleAutoMap, new KeyboardState(), PadDown(Buttons.Back)));
        Assert.False(plain.Pressed(GameAction.CycleScale, new KeyboardState(), PadDown(Buttons.Back)));
        Assert.Contains("Start+Back", UiText.Table.Get("game.f11_scale_esc_menu.pad"));
    }

    // --- C23 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C23_SlotPickerLbRb_FirstLast_TreasuryLtRt_TopFoot_StStepsAndAClick()
    {
        Assert.Equal([Keys.Home, Keys.End], SaveSlotScreen.PadKeys([Buttons.LeftShoulder, Buttons.RightShoulder]));
        Assert.Equal([Keys.Home, Keys.End], TreasuryDialog.PadEndKeys([Buttons.LeftTrigger, Buttons.RightTrigger, Buttons.A]));
        Assert.True(Game1.FieldPadClicks([Buttons.DPadLeft], rightTriggerHeld: false));
        Assert.True(Game1.FieldPadClicks([Buttons.LeftThumbstickDown], rightTriggerHeld: false));
        Assert.True(Game1.FieldPadClicks([Buttons.A], rightTriggerHeld: false));
        Assert.False(Game1.FieldPadClicks([Buttons.A], rightTriggerHeld: true)); // RT + A: the command list
        Assert.False(Game1.FieldPadClicks([Buttons.X, Buttons.LeftShoulder], rightTriggerHeld: false));
    }
}
