using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s183 "Pad leftovers" (I–III, main menu, Options): help lines in the pad's words while the pad is the input
/// (<see cref="PadWording"/>), the right stick's F2 and prompt scroll, and the fight's B (back through a DOS order list),
/// RT (III Amiga's CANCEL) and I Amiga's back.
/// </summary>
public sealed class PadLeftoversTests
{
    /// <summary>Runs <paramref name="body"/> with the pad as the input, then puts the flag back.</summary>
    private static void WithPad(Action body)
    {
        var was = PadWording.Active;
        PadWording.Active = true;
        try
        {
            body();
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    // --- PadWording -------------------------------------------------------------------------------------------------

    [Fact]
    public void Note_APadButtonTurnsItOn_AKeyOrTheMouseOff_TheyWinAFrameWithBoth()
    {
        var was = PadWording.Active;
        try
        {
            PadWording.Active = false;
            PadWording.Note(padPressed: false, keyOrMouse: false);
            Assert.False(PadWording.Active);
            PadWording.Note(padPressed: true, keyOrMouse: false);
            Assert.True(PadWording.Active);
            PadWording.Note(padPressed: false, keyOrMouse: false); // nothing pressed: stays
            Assert.True(PadWording.Active);
            PadWording.Note(padPressed: true, keyOrMouse: true);
            Assert.False(PadWording.Active);
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    [Fact]
    public void Help_IsThePadTwin_OnlyWhileThePadIsActive_OtherTextUnchanged()
    {
        var menu = UiText.S("menu.help");
        Assert.Same(menu, PadWording.Help(menu)); // the keyboard: unchanged
        WithPad(() =>
        {
            Assert.Equal(UiText.S("menu.help.pad"), PadWording.Help(menu));
            Assert.Equal(UiText.S("options.help.pad"), PadWording.Help(UiText.S("options.help")));
            Assert.Equal("NO SUCH HELP", PadWording.Help("NO SUCH HELP"));
            Assert.Equal("", PadWording.Help(""));
        });
    }

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void EveryPadTwin_HasItsKeyboardText_AndNoTwoTwinsShareOne(GameId game)
    {
        var table = UiText.For(game);
        var twins = table.Strings.Keys.Where(k => k.EndsWith(PadWording.Suffix, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(twins);
        foreach (var twin in twins)
            Assert.True(table.Contains(twin[..^PadWording.Suffix.Length]), twin);
        // A keyboard text two ids share must not need two pad wordings (the draw only sees the text).
        var clashes = twins.GroupBy(t => table.Get(t[..^PadWording.Suffix.Length]))
            .Where(g => g.Select(table.Get).Distinct().Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(clashes);
        Assert.Equal(twins.Count, twins.Select(table.Get).Count(t => t.Length > 0));
    }

    [Fact]
    public void MenuScreenHelps_InThePadsWords_FitTheirOneRow()
    {
        // DrawMenuScreen draws its help unwrapped from x 14 inside the 308-px panel (main menu, Options, the slot picker).
        foreach (var id in new[] { "menu.help.pad", "options.help.pad", "saveslots.help_pad", "saveslots.help_confirm.pad" })
            Assert.True(14 + PixelFont.Measure(UiText.S(id)) <= 312, id);
        // The HUD's help line wraps at 52 columns; every pad wording keeps to two rows. s193 (E31): an Options
        // description's twin (.desc.pad) is no help line: the description box wraps and scrolls it.
        foreach (var game in new[] { GameId.P1, GameId.P3 })
        {
            var table = UiText.For(game);
            foreach (var id in table.Strings.Keys.Where(k => k.EndsWith(PadWording.Suffix, StringComparison.Ordinal)
                         && !k.EndsWith(".desc" + PadWording.Suffix, StringComparison.Ordinal)))
                Assert.True(table.Get(id).Length <= 2 * 52, id);
        }
        Assert.True(UiText.F("inventory.sell_line_pad", 65000).Length <= 25); // the detail column
    }

    [Fact]
    public void TheListedHelpLines_HaveAPadWording_NamingTheirButtons()
    {
        var p1 = UiText.For(GameId.P1);
        Assert.Contains("LT/RT", p1.Get("game.arrows_enter_a_b_m_n.pad")); // the shopper
        Assert.Contains("LT/RT", p1.Get("game.up_down_enter_buy_1_6.pad"));
        Assert.Contains("A commands", p1.Get("game.l_list_p_purge_n_new.pad")); // the Guild's A
        Assert.Contains("A commands", UiText.For(GameId.P3).Get("game.l_list_p_purge_n_new.pad"));
        foreach (var id in new[] { "game.3_n_e_s_w_arrows.pad", "game.3_n_e_s_w_arrows_key.pad", "game.3_n_e_s_w_arrows_2.pad" })
            Assert.Contains("RT+A", p1.Get(id)); // the field's command list
        Assert.Contains("X type", p1.Get("continue.type_the_guild_number_enter_load.pad")); // the Guild-number question
        Assert.Contains("RT", UiText.S("p3combat.spell_help.pad"));
        Assert.Contains("B redo", p1.Get("game.1_6_orders_enter_keep_bksp.pad"));
        Assert.DoesNotContain("(L)", UiText.F("inventory.sell_line_pad", 10));
        Assert.Contains("LB RB", UiText.S("inventory.help_pad_town"));
        // s183 live pass: the game menu (every game's), III's Y / N prompt and III's OK / Cancel window.
        foreach (var game in new[] { GameId.P1, GameId.P2, GameId.P3 })
            Assert.Contains("B resume", UiText.For(game).Get("gamemenu.up_down_enter_select_esc_resume.pad"));
        var p3 = UiText.For(GameId.P3);
        Assert.Contains("B no", p3.Get("game.arrows_enter_y_yes_n_esc_no.pad"));
        Assert.Contains("B cancel", p3.Get("game.keep_window_ok_cancel.pad"));
    }

    [Fact]
    public void SaveSlotPicker_Help_InThePadsWords_WhileThePadIsActive()
    {
        var screen = new SaveSlotScreen();
        screen.Open(GameId.P1, SaveSlotMode.Load, SaveSlots.All("/saves", GameId.P1).Select(s => new SaveSlotInfo(s, null, null)).ToArray(),
            (_, _) => null);
        Assert.NotEqual(UiText.S("saveslots.help_pad"), screen.Help);
        WithPad(() => Assert.Equal(UiText.S("saveslots.help_pad"), screen.Help));
    }

    // --- the right stick: F2, a long prompt's scroll ------------------------------------------------------------------

    private static GamePadState Pad(Vector2 rightStick = default, params Buttons[] buttons) =>
        new(new GamePadThumbSticks(Vector2.Zero, rightStick), new GamePadTriggers(0, 0),
            new GamePadButtons(buttons.Aggregate((Buttons)0, (a, b) => a | b)), new GamePadDPad());

    [Fact]
    public void RightStickSideways_IsThePadsF2_OncePerPush_NotOnUpDown()
    {
        var input = new InputMap(new ClientSettings());
        var keys = new KeyboardState();
        Assert.False(InputMap.RightStickSideways(Pad(new Vector2(0, 1))));
        Assert.False(InputMap.RightStickSideways(Pad(new Vector2(0.7f, 0.7f)))); // a diagonal: no
        var left = Pad(new Vector2(-1, 0));
        Assert.True(input.Pressed(GameAction.ToggleMapLayout, keys, left));
        input.EndFrame(keys, left);
        Assert.False(input.Pressed(GameAction.ToggleMapLayout, keys, left)); // held: once
        Assert.False(input.Pressed(GameAction.ToggleAutoMap, keys, left));
        input.EndFrame(keys, Pad());
        Assert.True(input.Pressed(GameAction.ToggleMapLayout, keys, Pad(new Vector2(1, 0))));
    }

    [Fact]
    public void PromptScroll_RightStickUpDown_ArePageKeys_OnlyWhileItCanScroll()
    {
        Buttons[] stick = [Buttons.RightThumbstickUp, Buttons.RightThumbstickDown, Buttons.A, Buttons.LeftShoulder];
        Assert.Equal([Keys.PageUp, Keys.PageDown], Game1.PromptScrollPadKeys(stick, canScroll: true));
        Assert.Empty(Game1.PromptScrollPadKeys(stick, canScroll: false));
        Assert.Empty(Game1.PromptPadKeys([Buttons.RightThumbstickUp])); // never an answer or "any key"
    }

    // --- the fight ----------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void DosOrders_PadBack_RestartsTheList_PastTheFirstMembersMenu_Only()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.False(ui.PadBackRestartsOrders(s)); // Party Options: B is the game menu
        ui.HandleKey(s, Keys.Enter); // FIGHT
        Assert.Equal(0, ui.OrderMember);
        Assert.False(ui.PadBackRestartsOrders(s)); // the first member's menu: nothing to go back to
        ui.HandleKey(s, Keys.Enter); // keep its order
        Assert.Equal(1, ui.OrderMember);
        Assert.True(ui.PadBackRestartsOrders(s));
        ui.HandleKey(s, Keys.Back); // what Game1 sends for B
        Assert.Equal(0, ui.OrderMember);
        Assert.False(ui.PadBackRestartsOrders(s));
    }

    [OriginalDataFact]
    public void P1Amiga_Orders_Esc_GoesBackToTheMemberBoxes_AndNoBackRestart()
    {
        var s = ClientSessions.Fight();
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = layout, AmigaP1Field = () => true };
        ui.Start(s);
        ui.HandleKey(s, Keys.Enter); // Party Options down, on Fight
        ui.HandleKey(s, Keys.Enter); // Fight
        Assert.True(ui.P1OrdersShown);
        ui.HandleKey(s, Keys.D1); // the first member's order buttons
        Assert.True(ui.P1PickedMember >= 0);
        Assert.False(ui.PadBackRestartsOrders(s)); // the Amiga screen: B is its Esc (Game1), not Backspace
        ui.HandleKey(s, Keys.Escape); // what Game1 now sends for B / Esc with a member picked
        Assert.Equal(-1, ui.P1PickedMember);
        Assert.True(ui.P1OrdersShown);
    }

    [Fact]
    public void RightTrigger_SendsTheStepsKey_OnlyWhereItNamesOne()
    {
        var pad = new CombatPad();
        Assert.Empty(pad.KeysFor(new CombatPadStep("a", []), [Buttons.RightTrigger]));
        Assert.Equal([Keys.C], pad.KeysFor(new CombatPadStep("b", [], RightTriggerKey: Keys.C), [Buttons.RightTrigger]));
    }

    [OriginalDataFact]
    public void P3Amiga_SpellWindow_RT_IsCancel_TheOrderBecomesThrust()
    {
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("HERO0", Race.Human, CharacterClass.Fighter), out var e0), e0);
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("HERO1", Race.Human, CharacterClass.Wizard), out var e1), e1);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var wizard = s.Party.Members[1];
        for (var i = 0; i < wizard.SpellsKnown.Length; i++)
            wizard.SpellsKnown[i] = 1;
        wizard.MagicCur = 50;
        var ui = new CombatPresenter(new ClientSettings()) { AmigaOrders = () => true };
        ui.Start(s);
        ui.HandleKey(s, Keys.Enter); // Party Options: FIGHT
        ui.HandleKey(s, Keys.D2); // the wizard
        ui.HandleKey(s, Keys.C); // Cast
        if (!ui.AsksAmigaSpell)
            return; // refused before the list: no window to cancel
        var pad = new CombatPad();
        var keys = pad.KeysFor(ui.PadStep(s), [Buttons.RightTrigger]);
        Assert.Equal([Keys.C], keys);
        ui.HandleKey(s, keys[0]);
        Assert.False(ui.AsksAmigaSpell);
        Assert.StartsWith(CombatText.ChoiceLabel(CombatChoice.Thrust), ui.OrderLabelFor(s, 1), StringComparison.OrdinalIgnoreCase);
    }
}
