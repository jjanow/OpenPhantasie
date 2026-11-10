using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s185 "Pad leftovers": Phantasie I's key-worded help lines have their pad wording (<see cref="PadWording"/>): I DOS
/// combat's Party Options and order lines, the Y / N steps, the town, Guild, Inspect, Cast / Potion / Use item, Time lag
/// and DOS save dialogs, and the shared lines I's dialogs show.
/// </summary>
public sealed class PadWordingP1Tests
{
    /// <summary>Help lines looked up by their shown text (<see cref="PadWording.Help"/>).</summary>
    public static readonly string[] HelpIds =
    [
        "timelag.0_fastest_9_slowest_esc_cancel", "timelag.y_sound_on_n_sound_off", "health.any_key_to_continue",
        "cast.1_9_choose_up_down_more", "cast.1_9_choose_esc_cancel", "potion.1_9_choose_esc_cancel",
        "useitem.1_9_choose_esc_cancel", "distribute.up_down_enter_give_1_6", "useitem.up_down_scroll_any_key_back",
        "inspect.up_down_scroll_any_key_back", "dossave.type_folder_del_clear_enter_ok", "dossave.y_yes_n_no",
        "dossave.up_down_scroll_enter_close", "dossave.enter_close", "towndlg.arrows_enter_or_1_6_choose",
        "towndlg.type_amount_back_clear_digit_enter", "towndlg.1_9_learn_up_down_more",
        "guild.arrows_choose_enter_go_esc_cancel", "guild.arrows_enter_or_y_yes_n", "guild.arrows_choose_enter_add_esc_done",
        "member.arrows_choose_enter_examine_esc_done", "guild.1_6_drop_esc_done", "guild.a_agree_n_not",
        "guild.1_6_choose_esc_done", "member.1_6_race_esc_cancel", "member.1_6_class_esc_back", "member.y_keep_n_roll_again",
        "member.type_name_enter_accept_esc_cancel", "member.space_done", "member.arrows_choose_enter_rename_esc_done",
        "member.arrows_choose_enter_purge_esc_done", "member.arrows_scroll_space_done", "inspect.any_key",
        "inspect.arrows_enter", "inspect.1_7_inspect_esc_done", "inspect.arrows_enter_page_esc_back",
        "inspect.up_down_move_enter_esc_back", "treasury.help", "ui.shift_skip", "ui.arrows_select_enter_compare",
        "game.arrows_enter_or_y_yes_n", "game.enter_continue", "game.f11_scale_esc_menu",
        "game.arrows_choose_enter_select_shift_auto",
        // shared ids I's dialogs show (their twins in I's table)
        "gamemenu.y_yes_n_esc_back", "useitem.yes_no_help", "useitem.ok_abort_help", "transfer.type_file_del_clear_enter_ok",
        "transfer.y_keep_n_skip_esc_cancel", "continue.any_key",
    ];

    /// <summary>Templates, or lines shown as the message: their callers pick the twin (<see cref="PadWording.F"/>).</summary>
    public static readonly (string Id, object[] Args)[] Templates =
    [
        ("combat.party_options_x_arrows_enter", ["FIGHT"]),
        ("combat.x_spell_x_enter", ["ARAGORN", "1"]),
        ("combat.x_x_orders_x_enter_keep", ["ARAGORN", "THRUST", "1 THRUST 2 ATTACK"]),
        ("game.arrows_enter_or_1_x", [4]),
    ];

    private static StringTable P1 => UiText.For(GameId.P1);

    public static TheoryData<string> AllIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in HelpIds.Concat(Templates.Select(t => t.Id)))
            data.Add(id);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void EachCoveredId_HasAPadTwin_ThatItsTextResolvesTo(string id)
    {
        var table = P1;
        Assert.True(table.Contains(id + PadWording.Suffix), id + " has no pad twin");
        var pad = table.Get(id + PadWording.Suffix);
        Assert.NotEqual(table.Get(id), pad);
        Assert.Equal(pad, PadWording.PadTextFor(table, table.Get(id)));
    }

    [Fact]
    public void TheTwins_AreFoundUnderIIAndIII_WhoseTablesExtendI()
    {
        foreach (var game in new[] { GameId.P2, GameId.P3 })
        {
            var table = UiText.For(game);
            foreach (var id in HelpIds)
                Assert.NotNull(PadWording.PadTextFor(table, table.Get(id)));
        }
    }

    [Fact]
    public void HelpLines_ShowTheirTwin_OnlyWhileThePadIsActive()
    {
        var was = PadWording.Active;
        try
        {
            foreach (var id in HelpIds)
            {
                var text = UiText.S(id);
                PadWording.Active = false;
                Assert.Same(text, PadWording.Help(text));
                PadWording.Active = true;
                Assert.Equal(UiText.S(id + PadWording.Suffix), PadWording.Help(text));
            }
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    [Fact]
    public void Templates_FillTheirTwin_OnlyWhileThePadIsActive()
    {
        var was = PadWording.Active;
        try
        {
            foreach (var (id, args) in Templates)
            {
                PadWording.Active = false;
                Assert.Equal(UiText.F(id, args), PadWording.F(id, args));
                PadWording.Active = true;
                var pad = PadWording.F(id, args);
                Assert.Equal(UiText.F(id + PadWording.Suffix, args), pad);
                Assert.NotEqual(UiText.F(id, args), pad);
            }
            // the Party Options line keeps the lit option; the order line the member, the order and the menu
            Assert.Equal("PARTY OPTIONS: FIGHT  (D-pad, A)", PadWording.F("combat.party_options_x_arrows_enter", "FIGHT"));
            Assert.StartsWith("ARAGORN (THRUST): 1 THRUST 2 ATTACK",
                PadWording.F("combat.x_x_orders_x_enter_keep", "ARAGORN", "THRUST", "1 THRUST 2 ATTACK"));
            // an id with no twin is its own text, pad or not
            Assert.Equal(UiText.F("combat.x_which_rank_1_x", "ARAGORN", 3), PadWording.F("combat.x_which_rank_1_x", "ARAGORN", 3));
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    [Fact]
    public void NoTwin_NamesAKeyboardKey()
    {
        var keyWords = new[] { "Enter", "Esc", "Space", "Bksp", "Arrows", "arrows", "Shift", "Return", "PgUp", "any key", "Any key" };
        foreach (var id in HelpIds.Concat(Templates.Select(t => t.Id)))
        {
            var pad = P1.Get(id + PadWording.Suffix);
            Assert.DoesNotContain(keyWords, w => pad.Contains(w, StringComparison.Ordinal));
        }
    }
}
