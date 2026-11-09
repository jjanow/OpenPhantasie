using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The Guild treasury in IV's screen (<c>docs/plan/11-guild-treasury.md</c> section 5): the bank's treasury window (every
/// ledger line, scrolling), the gold lines in town and in the field, the shipwright's spendable gold and the profile
/// without its account. Off, the original lines.
/// </summary>
public class P4TreasuryScreenTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly GameOptions Trip = Code.With(RuleOptionRegistry.Treasury.Id, "trip");

    private static PanelExtras Extras() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!.CommandPanels!.Extras;

    private static int Width(string t) => 8 * t.Length;

    private static Phantasie4Campaign InTown(GameOptions o)
    {
        var game = P4Preview.StartInTown(1, o);
        while (game.Town.Prompt == P4TownPrompt.Wait)
            game.Town.Continue();
        return game;
    }

    private static List<string> Texts(P4DungeonScreen s) => [.. s.BoxLines().Select(l => l.Text)];

    [OriginalDataFact]
    public void Bank_ShowsTheTreasuryWindow_EveryLine_AndScrollsWhenLong()
    {
        var game = InTown(Trip);
        var t = game.Party.Treasury;
        for (var i = 0; i < 30; i++)
        {
            t.Add(100 + i, TreasuryEntryKind.Trip);
            if (i % 3 == 0)
                t.StartTrip();
        }
        game.Town.Enter(P4Building.Bank);
        Assert.Equal(P4BankView.Treasury, game.Town.BankWindow!.View);
        var lines = game.Town.TreasuryLines!;
        var bank = Extras().Bank!;
        var rows = P4DungeonScreen.TreasuryRows(bank, lines, Width);
        // Nothing dropped: every non-empty line is a row, in order.
        Assert.Equal(lines.Where(l => l.Length > 0), rows.Select(r => r.Text).Where(r => r.Length > 0));
        Assert.True(rows.Count > P4DungeonScreen.TreasuryVisibleRows(bank)); // more than the box: it scrolls
        var frame = P4DungeonScreen.TreasuryFrame(bank);
        Assert.All(rows, r => Assert.True(r.X + Width(r.Text) <= frame.X + frame.Width, r.Text));
        // The text column shows the window's lines when the pack's window is not up; SPACE leaves.
        game.Town.Continue();
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
    }

    [OriginalDataFact]
    public void Bank_WithoutTheOption_KeepsTheAccounts()
    {
        var game = InTown(Code);
        game.Town.Enter(P4Building.Bank);
        Assert.Equal(P4BankView.Accounts, game.Town.BankWindow!.View);
        Assert.Null(game.Town.TreasuryLines);
    }

    [OriginalDataFact]
    public void TownGoldLine_ShowsSpendableGold_UnderTheTreasury()
    {
        var on = InTown(Trip);
        on.Party.Treasury.Add(4321, TreasuryEntryKind.Trip);
        var s = new P4DungeonScreen(on);
        Assert.Contains(TreasuryReport.GoldLine(on.Party.SpendableGold(on.Options), 0), Texts(s));

        var off = InTown(Code);
        off.Party.Gold = 77;
        Assert.Contains(UiText.F("p4.town.gold_x", 77), Texts(new P4DungeonScreen(off)));
    }

    [OriginalDataFact]
    public void PartyPanel_AddsTheGoldLine_AwayFromTown_OnlyUnderTheTreasury()
    {
        var on = P4Preview.Start(4, Trip);
        on.Party.Gold = 30;
        on.Party.Treasury.Add(500, TreasuryEntryKind.Trip);
        var lines = new P4DungeonScreen(on).PartyLines();
        Assert.Equal(on.Party.Members.Count + 1, lines.Count);
        Assert.Equal(TreasuryReport.GoldLine(530, 30), lines[^1]);

        var off = P4Preview.Start(4, Code);
        Assert.Equal(off.Party.Members.Count, new P4DungeonScreen(off).PartyLines().Count);
    }

    [OriginalDataFact]
    public void Profile_HidesTheAccount_UnderTheTreasury()
    {
        foreach (var (o, shows) in new[] { (Code, true), (Trip, false) })
        {
            var game = InTown(o);
            game.Party.Roster[0]!.Account = 0;
            game.Party.Roster[0]!.Score = 0;
            game.Town.Enter(P4Building.Guild);
            game.Town.Choose(0); // the roster
            game.Town.Choose(2); // profile
            game.Town.Choose(0);
            var m = game.Town.Profile!;
            var texts = Texts(new P4DungeonScreen(game));
            Assert.Equal(shows, texts.Contains(UiText.F("p4.profile.score_exp_gold_x_y_z", m.Score, m.Experience, m.Account)));
            Assert.Equal(!shows, texts.Contains(UiText.F("p4.profile.score_exp_x_y", m.Score, m.Experience)));
        }
    }
}
