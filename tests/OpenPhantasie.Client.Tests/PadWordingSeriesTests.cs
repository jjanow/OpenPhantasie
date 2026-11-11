using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s185 "Pad leftovers": II's and III's help lines and the shared screens' (lineage, game menu, transfer, Use item, III's
/// training / Bank / Trade / fight windows / event pages, the update screen) in the pad's words (<see cref="PadWording"/>).
/// </summary>
public sealed class PadWordingSeriesTests
{
    /// <summary>The help ids given a <c>.pad</c> twin in s185 (all in the shared table).</summary>
    public static readonly string[] CoveredIds =
    [
        "options.update.help.available", "options.update.help.restart", "options.update.help.back",
        "lineage.kind_help", "lineage.pick_help", "lineage.race_help",
        "gamemenu.y_yes_n_esc_back",
        "transfer.type_file_del_clear_enter_ok", "transfer.y_keep_n_skip_esc_cancel",
        "useitem.ok_abort_help",
        "p3train.help",
        "p3town.bank_window_pick_help", "p3town.bank_window_amount_help", "p3town.bank_window_shares_help",
        "p3town.trade_to_help",
        "p3combat.orders_help", "p3combat.message_help", "p3combat.member_window_help",
        "game.event_page_any_key", "game.event_page_number",
    ];

    [Theory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void EachCoveredHelp_ShowsItsOwnTwin_InEveryGamesTable(GameId game)
    {
        var table = UiText.For(game);
        foreach (var id in CoveredIds)
        {
            Assert.True(table.Contains(id + PadWording.Suffix), id);
            // Equal, not just non-null: another id with the same keyboard text must not lend it its wording.
            Assert.Equal(table.Get(id + PadWording.Suffix), PadWording.PadTextFor(table, table.Get(id)));
            Assert.True(table.Get(id + PadWording.Suffix).Length <= 2 * 52, id); // the HUD's help line: two rows at most
        }
    }

    [Fact]
    public void TheUpdateScreensHelps_FitTheMenuScreensOneRow()
    {
        foreach (var id in new[] { "options.update.help.available.pad", "options.update.help.restart.pad", "options.update.help.back.pad" })
            Assert.True(14 + PixelFont.Measure(UiText.S(id)) <= 312, id);
    }

    [Fact]
    public void ATemplatedHelp_TakesItsTwinsTemplate_OnlyWhileThePadIsActive()
    {
        var was = PadWording.Active;
        try
        {
            PadWording.Active = false;
            Assert.Equal(UiText.F("p3town.bank_window_shares_help", "1", "3"),
                PadWording.Format("p3town.bank_window_shares_help", "1", "3"));
            PadWording.Active = true;
            var pad = PadWording.Format("p3town.bank_window_shares_help", "1", "3");
            Assert.Equal(UiText.F("p3town.bank_window_shares_help.pad", "1", "3"), pad);
            Assert.StartsWith("1-3 shares", pad, StringComparison.Ordinal);
            Assert.True(PadWording.Shows(pad));
            // The experience-share question also names its pad controls (G37).
            var experience = PadWording.Format("game.x_x_shares", "1", "3");
            Assert.Equal(UiText.F("game.x_x_shares.pad", "1", "3"), experience);
            // The town picture draws a help only in the pad's words (Game1.PadHelpShown): the formatted one counts.
            Assert.True(PadWording.Shows(experience));
            Assert.True(PadWording.Shows(UiText.S("p3town.bank_window_pick_help")));
            Assert.False(PadWording.Shows(UiText.F("game.x_x_shares", "1", "3")));
            PadWording.Active = false;
            Assert.False(PadWording.Shows(pad));
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    [Fact]
    public void EventPageFooter_ShowsPadControlsOrSelection_AndPreservesKeyboardFooter()
    {
        var was = PadWording.Active;
        try
        {
            PadWording.Active = false;
            Assert.Equal("keyboard footer", Game1.EventPageFooter("keyboard footer", true, "> 2) SOUTH"));
            PadWording.Active = true;
            Assert.Equal(UiText.S("game.event_page_any_key.pad"), Game1.EventPageFooter("keyboard footer", true, null));
            Assert.Equal(UiText.S("game.event_page_number.pad"), Game1.EventPageFooter("keyboard footer", false, null));
            Assert.Equal("> 2) SOUTH", Game1.EventPageFooter("keyboard footer", false, "> 2) SOUTH"));
        }
        finally { PadWording.Active = was; }
    }

    [Fact]
    public void TheWordings_NameOnlyButtonsTheirScreensTake()
    {
        // The shares window: B is its CANCEL since s190 (pad fix list C11).
        Assert.EndsWith("B cancel", UiText.S("p3town.bank_window_shares_help.pad"), StringComparison.Ordinal);
        // Steps taking a digit only (A does nothing): the wheel's X, no A.
        foreach (var id in new[] { "lineage.kind_help.pad", "lineage.race_help.pad", "p3train.help.pad" })
        {
            Assert.Contains("X ", UiText.S(id), StringComparison.Ordinal);
            Assert.DoesNotContain("A ", UiText.S(id), StringComparison.Ordinal);
        }
        // Trade's receiver step has a cursor (B7), so A gives/sells the row and X has no wheel action.
        var trade = UiText.S("p3town.trade_to_help.pad");
        Assert.Contains("D-pad pick", trade, StringComparison.Ordinal);
        Assert.Contains("A give/sell", trade, StringComparison.Ordinal);
        Assert.DoesNotContain("X ", trade, StringComparison.Ordinal);
        // The Y / N steps (DialogPad.YesNoKeys): A = Y, X = N.
        Assert.Equal("A keep  X skip  B cancel", UiText.S("transfer.y_keep_n_skip_esc_cancel.pad"));
        Assert.Equal(UiText.S("pad.dialog_yes_no")["Pad: ".Length..], UiText.S("gamemenu.y_yes_n_esc_back.pad"));
    }
}
