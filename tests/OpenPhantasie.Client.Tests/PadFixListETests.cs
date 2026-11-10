using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s193: the pad fix list's E 29-32 (<c>docs/plan/14-pad-fix-list.md</c>): the pad hints on the screens that hid the help
/// (I Amiga's map line, I / III Amiga's combat screens, III Amiga's outdoors bar), the keyboard words with no pad twin, the
/// Options descriptions' pad wording, and "D-pad" for "Up/Down".
/// </summary>
public class PadFixListETests
{
    private static T WithPad<T>(bool active, Func<T> read)
    {
        var was = PadWording.Active;
        try
        {
            PadWording.Active = active;
            return read();
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    private static void AssertTwin(StringTable table, string id, string contains)
    {
        Assert.True(table.Contains(id + PadWording.Suffix), id + " has no pad twin");
        var pad = table.Get(id + PadWording.Suffix);
        Assert.Contains(contains, pad);
        Assert.Equal(pad, PadWording.PadTextFor(table, table.Get(id))); // its keyboard text finds it (no shared text)
    }

    // --- E29 ------------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void E29_IAmigaMapHint_NamesTheButtons_OnOneRowOfTheLine()
    {
        var hint = UiText.For(GameId.P1).Get("game.p1map_pad_hint");
        foreach (var word in new[] { "A fight", "Start", "RT+A", "R-stick" })
            Assert.Contains(word, hint);
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.MapMessage!;
        var font = FontSources.Named(layout.Font, layout.FontFile) ?? PixelFont.Font;
        var rows = UiOverlays.P1MapLineRows(layout.Line!, font, "", hint);
        Assert.Single(rows); // the message's own row, nothing stacked over the map
        Assert.Equal(layout.Line!.TopEdge, rows[0].Y);
    }

    [Fact]
    public void E29_IIIAmigaOutdoorsHelp_NamesStart_InThePadsWords()
    {
        var table = UiText.For(GameId.P3);
        AssertTwin(table, "game.outside_bar_help", "Start menu");
        AssertTwin(table, "game.outside_bar_dungeon_help", "Start menu");
        Assert.Contains("Back auto-map", table.Get("game.outside_bar_dungeon_help.pad"));
    }

    [OriginalDataFact]
    public void E29_AmigaCombatHelps_HaveTwins_ThatFitTheHintRow()
    {
        var table = UiText.For(GameId.P1);
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        foreach (var id in new[] { "p1field.help", "p1field.orders_help", "p1field.order_help", "p1field.spell_help",
                     "p1field.target_help", "p3field.help", "p3combat.rank_help" })
        {
            AssertTwin(table, id, "");
            Assert.True(table.Get(id + PadWording.Suffix).Length <= UiOverlays.P1PadHintColumns(layout), id);
        }
        Assert.Contains("RT cancel", table.Get("p3combat.spell_help.pad")); // III's spell window, already twinned
        Assert.True(UiOverlays.FieldPadHintFits(AmigaFieldWindow.None, table.Get("p3field.help.pad")));
        Assert.True(UiOverlays.FieldPadHintFits(AmigaFieldWindow.MonsterNames, "Press A")); // window 23 ends at y 191
        Assert.False(UiOverlays.FieldPadHintFits(AmigaFieldWindow.PartyHealth, "Press A")); // window 22 reaches y 199
    }

    [OriginalDataFact]
    public void E29_IAmigaFieldHint_TakesTheTopRow_NeverUnderAMenuOrOverTheTitles()
    {
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        Assert.Equal((layout.Field.Y + 1, false), UiOverlays.P1PadHintPlace(layout, [], partyOptions: true, menuOpen: false));
        Assert.Null(UiOverlays.P1PadHintPlace(layout, [], partyOptions: true, menuOpen: true));
        var tall = Enumerable.Repeat(layout.Field.H, 3).ToList(); // groups up to the field's top
        Assert.Null(UiOverlays.P1PadHintPlace(layout, tall, partyOptions: true, menuOpen: false)); // the titles are the menus
        Assert.True(UiOverlays.P1PadHintPlace(layout, tall, partyOptions: false, menuOpen: false)!.Value.OnBar); // orders
        Assert.True(layout.Field.Y + 1 + UiOverlays.P1HitLinePitch <= 23); // above every requester (orders box at y 23)
    }

    [OriginalDataFact]
    public void E29_IAmigaFight_HintFollowsTheStep_OnlyWhileThePadIsTheInput()
    {
        var s = ClientSessions.Fight();
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        var ui = new CombatPresenter(new ClientSettings()) { P1Layout = layout, AmigaP1Field = () => true };
        ui.Start(s);
        Assert.Null(WithPad(false, () => ui.AmigaPadHint(s)));
        Assert.Equal(UiText.S("p1field.help.pad"), WithPad(true, () => ui.AmigaPadHint(s)));
        ui.HandleKey(s, Keys.Enter); // Party Options down, on Fight
        ui.HandleKey(s, Keys.Enter); // Fight
        Assert.True(ui.P1OrdersShown);
        Assert.Equal(UiText.S("p1field.orders_help.pad"), WithPad(true, () => ui.AmigaPadHint(s)));
        ui.HandleKey(s, Keys.D1);
        Assert.Equal(UiText.S("p1field.order_help.pad"), WithPad(true, () => ui.AmigaPadHint(s)));
    }

    // --- E30 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E30_GameMenuRows_NameLbRb_AndNoFKeys_ForThePad()
    {
        // the test table is I's (GameTextSetup); switching it would drop Core's fallback tables
        var pad = WithPad(true, () => GameMenuDialog.EntriesFor(GameId.P1));
        Assert.EndsWith("LB", pad.Single(e => e.Choice == GameMenuChoice.Save).Label);
        Assert.EndsWith("RB", pad.Single(e => e.Choice == GameMenuChoice.Load).Label);
        Assert.DoesNotContain(pad, e => e.Label.Contains(" F", StringComparison.Ordinal) && e.Label.TrimEnd()[^1] is >= '0' and <= '9');
        Assert.EndsWith("F5", WithPad(false, () => GameMenuDialog.EntriesFor(GameId.P1)).Single(e => e.Choice == GameMenuChoice.Save).Label);
        AssertTwin(UiText.For(GameId.P2), "gamemenu.import_dos_save_f7", "Transfer from I"); // II's F7 row
        Assert.Equal("Transfer from I", UiText.For(GameId.P2).Get("gamemenu.import_dos_save_f7.pad"));
    }

    [Fact]
    public void E30_DosOrderPrompts_AndKeyLines_HaveTwins()
    {
        var p1 = UiText.For(GameId.P1);
        foreach (var id in new[] { "combat.x_which_rank_1_x", "combat.x_heal_whom_1_x", "combat.x_rank_front_middle_back" })
            Assert.EndsWith("(D-pad, A)", p1.Get(id + PadWording.Suffix));
        AssertTwin(p1, "ui.press_enter", "A");
        AssertTwin(p1, "ui.inventory_v", "(X)");
        AssertTwin(p1, "ui.bestiary_b", "(Y)");
        AssertTwin(p1, "game.distribute_modern", "Press X");
        AssertTwin(p1, "options.update.installed", "Press A");
        AssertTwin(p1, "options.update.help.restart", "B");
        AssertTwin(UiText.For(GameId.P3), "p3town.bank_window_help", "D-pad pick");
        Assert.False(p1.Contains("combat.press_any_key.pad")); // a sourced line: a pad-only text instead (no "..." twin)
        Assert.Equal("Press A", p1.Get("pad.combat.press_a"));
    }

    [Fact]
    public void E30_TheWelcomeLine_KeepsEachGamesTown_ForThePad()
    {
        foreach (var (game, town) in new[] { (GameId.P1, "Pelnor"), (GameId.P2, "Pippacott"), (GameId.P3, "Pendragon") })
        {
            var table = UiText.For(game);
            AssertTwin(table, "game.welcome_to_pelnor_g_enters_the", town);
            Assert.DoesNotContain("G enters", table.Get("game.welcome_to_pelnor_g_enters_the.pad"));
        }
    }

    [Fact]
    public void E30_TheSidebarsMoveRow_NamesRtA_AndFitsTheSidebar()
    {
        var p1 = UiText.For(GameId.P1);
        AssertTwin(p1, "ui.3_n_e_s_w_move", "RT+A commands");
        Assert.True(PixelFont.Measure(p1.Get("ui.3_n_e_s_w_move.pad")) <= UiOverlays.FieldPartyArea.Width - 6);
    }

    [OriginalDataFact]
    public void E30_DosOrders_BIsTheGameMenu_OnTheFirstMembersMenu_ThenRedo()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.False(ui.OnFirstDosOrderMenu(s)); // Party Options
        ui.HandleKey(s, Keys.Enter); // FIGHT
        Assert.True(ui.OnFirstDosOrderMenu(s));
        Assert.False(ui.PadBackRestartsOrders(s));
        ui.HandleKey(s, Keys.Enter); // keep its order
        Assert.False(ui.OnFirstDosOrderMenu(s));
        Assert.True(ui.PadBackRestartsOrders(s));
        var p1 = UiText.For(GameId.P1);
        AssertTwin(p1, "game.1_6_orders_enter_keep_esc", "B game menu");
        AssertTwin(p1, "p3combat.dos_rank_menu_help", "B Two");
        Assert.Contains("B redo", p1.Get("game.1_6_orders_enter_keep_bksp.pad"));
        // the new keyboard lines stay clickable as the old (DrawHud's tokens)
        var first = PanelMouse.Hotkeys(p1.Get("game.1_6_orders_enter_keep_esc"), commandList: true).Select(h => h.Key).ToList();
        Assert.Contains(Keys.Enter, first);
        Assert.Contains(Keys.Escape, first);
        var rank = PanelMouse.Hotkeys(p1.Get("p3combat.dos_rank_menu_help"), commandList: true).Select(h => h.Key).ToList();
        Assert.Contains(HitTest.Digit(1), rank); // "1 One" / "2 Two" click as the digits
        Assert.Contains(HitTest.Digit(2), rank);
        Assert.Contains(Keys.Back, rank);
    }

    // --- E31 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E31_OptionsDescriptions_TakeTheirPadTwin()
    {
        var table = UiText.Table;
        AssertTwin(table, "options.window_scale.desc", "Start+Back");
        AssertTwin(table, "options.title_screen.desc", "B or Back");
        AssertTwin(table, "options.auto_map.desc", "Back hides");
        AssertTwin(table, "options.combat_speed.desc", "LT");
        var o = new OptionsScreen(new ClientSettings(), _ => []);
        o.Open();
        o.HandleKeys([Keys.Down]); // WINDOW SCALE
        Assert.Contains(WithPad(true, () => o.Detail()), l => l.Text.Contains("Start+Back", StringComparison.Ordinal));
        Assert.Contains(WithPad(false, () => o.Detail()), l => l.Text.Contains("F11", StringComparison.Ordinal));
    }

    // --- E32 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void E32_PathAndWarningsHelp_SayDPad()
    {
        Assert.Contains("D-pad move", UiText.Table.Get("import.help.path.pad"));
        Assert.Equal("D-pad scroll  A close", UiText.For(GameId.P1).Get("dossave.up_down_scroll_enter_close.pad"));
    }
}
