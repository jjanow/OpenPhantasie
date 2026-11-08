using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s163: the inn's rows, the bank's tables and the ending's message window in the 352-row windows under their pictures
/// (FORMAT.md 6.9 "Building windows under the picture"): the pack's numbers from P4.X, Core's rows and the Client's layout.
/// </summary>
public class P4BuildingWindowTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    private static PanelExtras Extras() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!.CommandPanels!.Extras;

    private static int Width(string t) => 8 * t.Length;

    private static Phantasie4Campaign InTown()
    {
        var game = P4Preview.StartInTown(1, Code);
        while (game.Town.Prompt == P4TownPrompt.Wait)
            game.Town.Continue();
        return game;
    }

    [OriginalDataFact]
    public void Pack_HoldsTheInnBankAndEndingWindows()
    {
        var x = Extras();
        var inn = x.Inn!;
        Assert.Equal((8, 210, 296, 4, 16, 16, 212), (inn.BoxX, inn.BoxY, inn.BoxWidth, inn.BoxPad, inn.RowStep, inn.TextX, inn.TextY));
        Assert.Equal(("p4x.0x3f6f0", "p4x.0x3f6fc"), (inn.Slept.Label, inn.MpUp.Label));
        var bank = x.Bank!;
        Assert.Equal(new PanelRect(0, 180, 312, 172), bank.Clear);
        Assert.Equal(new PanelRect(32, 194, 248, 116), bank.Accounts.Frame);
        Assert.Equal((208, 5), (bank.Accounts.Columns["balance"], bank.Accounts.Digits));
        Assert.Equal((new PanelRect(0, 180, 312, 16), 0), (bank.Band, bank.BandColour));
        Assert.Equal((272, 240, 20, 4, 4, "  "), (bank.Buttons.X, bank.Buttons.Y, bank.Buttons.Step, bank.Buttons.Count,
            bank.Buttons.Colour, bank.Buttons.Pad));
        Assert.Equal([32, 96, 160, 224], new[] { "name", "balance", "limit", "amount" }.Select(c => bank.Withdraw.Columns[c]));
        Assert.Equal((68, 336, 8), (bank.Gold.X, bank.Gold.Y, bank.Gold.Digits));
        var end = x.EndMessage!;
        Assert.Equal((new PanelRect(160, 68, 312, 52), 232, 14), (end.Window, end.LowerOffset, end.LineStep));
        Assert.Equal((68, 68, 300, 300), (end.YFor(1), end.YFor(6), end.YFor(4), end.YFor(9)));
        Assert.Equal(20, end.SceneY["p4.ending.street"]);
    }

    [OriginalDataFact]
    public void Inn_EachSleeperOnTheirRow_TheMpLineAfterSlept()
    {
        var game = InTown();
        game.Town.Enter(P4Building.Inn);
        var rows = game.Town.InnRows!;
        Assert.Equal(game.Town.Party.Members.Count(m => m.Alive), rows.Count);
        Assert.All(rows, r => Assert.Equal(game.Town.Party.Members[r.Member].Name, r.Name));
        var inn = Extras().Inn!;
        var (text, height) = P4DungeonScreen.InnText(inn, [new P4InnRow(0, "BRAND", false), new P4InnRow(2, "ELWYN", true)], 4,
            s => s.Label == inn.Slept.Label ? " slept." : " MP went up!", Width);
        Assert.Equal([(16, 212, "BRAND slept."), (16, 244, "ELWYN slept. MP went up!")], text);
        Assert.Equal(4 + 16 * 4, height);
        // A row past the box (296 − 8) wraps at a space; the later rows and the box move down (nothing dropped).
        (text, height) = P4DungeonScreen.InnText(inn, [new P4InnRow(0, new string('A', 30), true), new P4InnRow(1, "B", false)], 2,
            s => s.Label == inn.Slept.Label ? " slept." : " MP went up!", Width);
        Assert.Equal([212, 228, 244], text.Select(t => t.Y));
        Assert.Equal(4 + 16 * 3, height);
        game.Town.Continue();
        Assert.Null(game.Town.InnRows);
    }

    [OriginalDataFact]
    public void Bank_WithdrawListsEveryMember_DepositShowsThePerShareAmount()
    {
        var game = InTown();
        var town = game.Town;
        town.Enter(P4Building.Bank);
        Assert.Equal(P4BankView.Accounts, town.BankWindow!.View);
        town.Choose(1); // W: withdraw
        while (town.Prompt == P4TownPrompt.Number)
            town.EnterNumber(10);
        var view = town.BankWindow!;
        Assert.Equal(P4BankView.Withdraw, view.View);
        Assert.Equal(town.Party.Members.Count, view.Rows.Count);
        Assert.All(view.Rows, r => Assert.Equal(r.Limit > 0 ? 10 : null, r.Amount));
        var bank = Extras().Bank!;
        var text = P4DungeonScreen.BankText(bank, view, town.Party.Members, town.Party.Gold, null, null, s => s.Text, (_, t) => t);
        var first = view.Rows[0];
        Assert.Contains((32, 228, first.Name, false, 0), text);
        Assert.Contains((96, 228, P4DungeonScreen.PaddedAmount(first.Account, 5), false, 0), text);
        Assert.Contains((156, 180, bank.WithdrawHeading.Text, true, 15), text);
        town.Continue();
        town.Choose(0); // D: deposit the gold just drawn
        Assert.Equal((P4TownPrompt.Shares, P4BankView.Deposit), (town.Prompt, town.BankWindow!.View));
        var gold = town.Party.Gold;
        town.EnterShares([.. town.Party.Members.Select(_ => 1)]);
        Assert.Equal((long)Math.Truncate(gold / (0.0001 + town.Party.Members.Count)), town.BankWindow!.PerShare);
        text = P4DungeonScreen.BankText(bank, town.BankWindow, town.Party.Members, 0, null, null, s => s.Text, (_, t) => t);
        Assert.Contains(text, t => t.Y == 336 && t.Text.StartsWith(bank.PerShare.Text, StringComparison.Ordinal));
        Assert.Contains((196, 244, "1", false, 0), text);
    }

    [OriginalDataFact]
    public void ItemWindows_RowsAtTheOriginalsColumns()
    {
        var iw = Extras().ItemWindows!;
        Assert.Equal((2, 0, 14), (iw.LetterColour, iw.TextColour, iw.BackColour));
        var armory = iw.Windows["_armory"];
        Assert.Equal((32, 32, 6, 32, 44, 80, 192, 4, 240, 5), (armory.Rows.Top, armory.Rows.Step, armory.Rows.Count,
            armory.Rows.LetterX, armory.Rows.IconX, armory.Rows.NameX, armory.Rows.PointsX, armory.Rows.PointsDigits,
            armory.Rows.PriceX, armory.Rows.PriceDigits));
        Assert.Equal(("p4x.0x42583", 253, 224), (armory.More!.Label, armory.More.X, armory.More.Y));
        Assert.Equal([new PanelRect(16, 16, 280, 208), new PanelRect(245, 224, 51, 16)], armory.Frames);
        var text = P4DungeonScreen.ItemWindowText(iw, armory,
            [new(5, 'A', "Clothing", "   1", "    4", []), new(9, 'B', "Robes", " N-U", "    8", [])], "BRAND", "bought.",
            s => s.Text);
        Assert.Contains((32, 40, "A", 2), text);
        Assert.Contains((80, 40, "Clothing", 0), text);
        Assert.Contains((192, 72, " N-U", 0), text);
        Assert.Contains((240, 72, "    8", 0), text);
        Assert.Contains((20, 256, "BRAND", 0), text);
        Assert.Contains((32, 288, "bought.", 0), text);
        var trade = iw.Windows["_trade"];
        text = P4DungeonScreen.ItemWindowText(iw, trade, [new(0, null, "", null, null, []), new(7, 'B', "Darts", "  5", null, [0, 2])],
            "ODO", null, s => s.Text);
        Assert.Contains((16, 104, "B", 2), text);   // row 1: y 64 + 32 + 8
        Assert.Contains((56, 104, "Darts", 0), text);
        Assert.Contains((240, 104, "1", 0), text);
        Assert.Contains((256, 104, "3", 0), text);
        Assert.Contains((16, 16, "ODO", 0), text);
        Assert.DoesNotContain(text, t => t.Y == 72); // an empty slot draws nothing
        var share = iw.Windows["_distribute"];
        text = P4DungeonScreen.ItemWindowText(iw, share, [new(7, null, "Darts", "  5", null, [1], Lit: true)], null, null, s => s.Text);
        Assert.Contains((48, 40, "Darts", 15), text); // row 0 lit: ink 0 ^ 15
        Assert.Null(share.Rows.LetterX);
        // A name that would reach the points column takes the row's two lines, split where both are shortest.
        text = P4DungeonScreen.ItemWindowText(iw, armory, [new(80, 'A', "Halfling Strength Potion", null, "  300", [])], null, null,
            s => s.Text, Width);
        Assert.Contains((80, 32, "Halfling", 0), text);
        Assert.Contains((80, 48, "Strength Potion", 0), text);
    }

    [OriginalDataFact]
    public void EndingPages_SitInEndmsgsWindow_UpperOverTheStreetLowerUnderTheBanquet()
    {
        var game = P4Preview.StartEnding(3, Code);
        var town = game.Town;
        while (town.Prompt == P4TownPrompt.Wait && town.EndingBlock is null)
            town.Continue();
        var end = Extras().EndMessage!;
        var blocks = new HashSet<(int Block, P4TownScene Scene)>();
        while (town.Prompt == P4TownPrompt.Wait && town.EndingBlock is int block)
        {
            blocks.Add((block, town.Scene));
            town.Continue();
        }
        Assert.Contains(blocks, b => b.Scene == P4TownScene.EndingStreet && end.YFor(b.Block) == 68);
        Assert.Contains(blocks, b => b.Scene == P4TownScene.EndingBanquet && end.YFor(b.Block) == 300);
        Assert.Null(town.EndingBlock);
        var (y, h, rows) = P4DungeonScreen.EndMessageText(end, "p4.ending.banquet", 4, ["one", "two", "three"], Width);
        Assert.Equal((268, 52), (y, h));
        Assert.Equal([(16, 4, "one"), (16, 18, "two"), (16, 32, "three")], rows);
        (y, h, rows) = P4DungeonScreen.EndMessageText(end, "p4.ending.street", 1, ["one", new string('x', 20) + " " + new string('y', 20), "z"], Width);
        Assert.Equal((48, 52 + 14), (y, h));
        Assert.Equal(4, rows.Count);
    }
}
