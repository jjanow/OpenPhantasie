using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>III's town screens in the Client: the DOS bank list, Trade Items and training's skill picks.</summary>
public class P3TownScreenClientTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static GameSession Town(GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(4));
        s.StartNewGame(options ?? GameOptions.ForPreset(GameId.P3));
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("A", CharacterClass.Fighter), ("B", CharacterClass.Priest) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out _));
        return s;
    }

    private static string? Press(IModalDialog d, GameSession s, params Keys[] keys) => d.HandleKeys(keys, s);

    [OriginalDataTheory]
    [InlineData(126, 126)] // Scroll IV: the Amiga picture D/s6 (pack scroll.picture.126)
    [InlineData(121, null)] // Scroll I: text
    public void UseItem_ScrollIV_IsAPicture_TextScrollsAreNot(int item, int? picture)
    {
        var s = Town();
        s.LeaveGuild();
        s.EnterTown(1);
        var reader = s.Party.Members[0];
        reader.InventoryItemIds.Clear();
        reader.InventoryItemIds.Add(item);
        var d = new UseItemDialog(P3Data);
        Assert.Null(d.Open(s));
        Press(d, s, Keys.D1); // who
        Press(d, s, Keys.D1); // which item
        Assert.True(d.IsOpen);
        Assert.Equal(picture, d.PictureItem);
        Press(d, s, Keys.Space); // any key: back to the list, the picture is gone
        Assert.Null(d.PictureItem);
    }

    [OriginalDataFact]
    public void FoundItemHeader_NoMaximumUnderUnlimited_D3020()
    {
        var screen = new InventoryScreen();
        screen.Open(Town(), P3Data);
        Assert.Equal(P3Town.ManualFoundItemLimit, screen.StashCapacity); // the manual's 90
        var unlimited = new InventoryScreen();
        unlimited.Open(Town(GameOptions.ForPreset(GameId.P3).With("p3.found-items", "unlimited")), P3Data);
        Assert.Null(unlimited.StashCapacity); // header "SACK n", not n/2147483647
    }

    [OriginalDataFact]
    public void IIIPrompts_UseIIIsText_ExitSaveMapKeep()
    {
        var iii = UiText.For(GameId.P3);
        Assert.Equal("An exit. Will you take it?", iii.Get("core.prompt.an_exit_take_it"));
        Assert.Equal("Would you like to save this map?", iii.Get("core.prompt.save_dungeon_map"));
        Assert.Equal("Keep {0}?", iii.Get("core.treasure.you_find_x_keep_it"));
        Assert.Equal("AN EXIT. TAKE IT?", UiText.For(GameId.P1).Get("core.prompt.an_exit_take_it"));
    }

    [OriginalDataFact]
    public void Scrolls_ShowTheirRecordsAsPages_ScrollIVIsAPicture()
    {
        // planes.md §7: item 122 (Scroll II) shows records 1, 2, 3; 129 (Scroll VII) 7, 8, 9; 126 (Scroll IV) a picture.
        var two = UseItemDialog.ScrollLines(P3Data, 122, "Scroll II");
        Assert.Equal("Scroll II", two[0]);
        Assert.Equal(2, two.Count(l => l.Length == 0)); // three pages, an empty line between them
        var first = P3Data.Scrolls.Single(s => s.Id == 1).Lines.First(l => l.Trim().Length > 0).Trim();
        Assert.Equal(first, two[1]);
        var single = UseItemDialog.ScrollLines(P3Data, 132, "Scroll VIII"); // record 10
        Assert.DoesNotContain("", single);
        Assert.Equal(P3Data.Scrolls.Single(s => s.Id == 10).Lines.First(l => l.Trim().Length > 0).Trim(), single[1]);
        Assert.Equal(["Scroll IV", UiText.S("useitem.scroll_picture")], UseItemDialog.ScrollLines(P3Data, 126, "Scroll IV"));
    }

    [Fact]
    public void TownMenu_DosBankAndTrade_ChangeTheLists()
    {
        var m = new TownMenu { DosBank = true, Trade = true };
        Assert.Null(m.HandleKey(Keys.B));
        Assert.Equal(TownChoice.Withdraw, m.HandleKey(Keys.Enter)); // "Withdraw all"
        m.HandleKey(Keys.B);
        m.HandleKey(Keys.Down);
        Assert.Equal(TownChoice.Deposit, m.HandleKey(Keys.Enter)); // "Deposit some"
        m.Reset();
        while (m.BarCursor != 0)
            m.HandleKey(Keys.Left);
        m.HandleKey(Keys.Enter); // Option
        Assert.Equal(TownMenu.OptionLabels.Length + 1, m.SubLabels.Count); // + Trade items
        while (m.SubCursor > 2)
            m.HandleKey(Keys.Up);
        Assert.Equal(TownChoice.Trade, m.HandleKey(Keys.Enter));
    }

    [OriginalDataFact]
    public void Trade_GiverItemReceiver_ThenBackToWhoTrades()
    {
        var s = Town();
        s.LeaveGuild();
        var d = new TradeDialog();
        Assert.Null(d.Open(s));
        var a = s.Party.Members[0];
        var b = s.Party.Members[1];
        b.InventoryItemIds.RemoveAt(b.InventoryItemIds.Count - 1);
        var item = a.InventoryItemIds[0];
        Press(d, s, Keys.D1);
        Press(d, s, Keys.D1);
        Assert.Contains(d.Lines(s, P3Data), l => l.StartsWith("7 "));
        Press(d, s, Keys.D2);
        Assert.Contains(item, b.InventoryItemIds);
        Assert.True(d.IsOpen);
        Assert.Equal(UiText.S("p3town.who_will_trade"), d.Lines(s, P3Data)[0]); // back to "Who will trade?" (Amiga)
        Assert.Equal("", Press(d, s, Keys.Escape));
        Assert.False(d.IsOpen);

        var dos = Town(GameOptions.ForPreset(GameId.P3).With("p3.trade", "dos"));
        dos.LeaveGuild();
        Assert.NotNull(new TradeDialog().Open(dos));
    }

    private static KeepWindowsLayout? Windows() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.KeepWindows;

    [OriginalDataFact]
    public void AmigaBank_DosRules_WithdrawsAll_DepositAsksAnEmptyField()
    {
        var s = Town(GameOptions.ForPreset(GameId.P3, OptionPreset.Dos));
        s.LeaveGuild();
        s.Party.GoldCarried = 0;
        foreach (var m in s.Party.Members)
            m.GoldInBank = 100;
        var d = new AmigaBankDialog();
        d.Open();
        if (Windows() is { } layout) // the pack's window 9 (only where the extracted art is present)
        {
            var view = d.View(s, P3Data, layout)!;
            Assert.Equal(layout.Bank, view.Window);
            Assert.Contains(view.Rows, r => r.Text == "200" && r.Highlight);       // members total in bank
            Assert.Contains(view.Rows, r => r.Text == UiText.S("p3town.bank_withdraw_all") && r.Highlight); // the pick
        }
        Assert.Equal("", Press(d, s, Keys.Enter));           // Withdraw all gold: closes, no message
        Assert.Equal(200, s.Party.GoldCarried);
        Assert.False(d.IsOpen);

        d.Open();
        Press(d, s, Keys.Down, Keys.O);                       // Deposit some gold: the field opens empty
        Assert.True(d.IsOpen);
        if (Windows() is { } layout2)
            Assert.Contains(d.View(s, P3Data, layout2)!.Rows, r => r.Text == UiText.S("p3town.bank_how_much") + "_");
        Assert.Equal("", Press(d, s, Keys.D5, Keys.D0, Keys.Enter));
        Assert.Equal(150, s.Party.GoldCarried);
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void AmigaBank_ManualRules_PicksOpenTheRemastersFollowUps()
    {
        var s = Town();
        s.LeaveGuild();
        var d = new AmigaBankDialog();
        d.Open();
        Press(d, s, Keys.D2);
        Assert.False(d.IsOpen);
        Assert.Equal(2, d.TakeFollow());
        Assert.Equal(0, d.TakeFollow());
        d.Open();
        Assert.Equal("", Press(d, s, Keys.C)); // CANCEL
        Assert.Equal(0, d.TakeFollow());
    }

    [OriginalDataFact]
    public void AmigaWithdraw_Window9Style_OkPicksAndTakes_CancelStepsBack()
    {
        var s = Town();
        s.LeaveGuild();
        s.Party.GoldCarried = 10;
        s.Party.Members[0].GoldInBank = 300;
        s.Party.Members[1].GoldInBank = 40;
        var layout = Windows();
        Assert.NotNull(layout); // the pack's window 9 must load here, or the view checks below test nothing
        var d = new WithdrawDialog { AmigaKeys = true };
        d.Open();
        var who = d.View(s, P3Data, layout!)!;
        Assert.Equal(layout!.Bank, who.Window);
        Assert.Equal(UiText.S("p3town.bank_who_withdraws"), who.Rows[0].Text);
        Assert.Contains(who.Rows, r => r.Text == "1 A" && r.Highlight);  // the cursor row
        Assert.Contains(who.Rows, r => r.Text == "2 B" && !r.Highlight);
        Assert.Contains(who.Rows, r => r.Text == "300" && r.Highlight);  // bank gold in the value colour
        Assert.Equal([Keys.D1, Keys.D2], who.Picks.Select(p => p.Key));
        Assert.True(who.Picks.All(p => who.Rows[p.Row].Text.EndsWith(s.Party.Members[p.Key - Keys.D1].Name)));
        Assert.Equal(UiText.S("p3town.bank_window_pick_help"), d.Help);

        Press(d, s, Keys.O);                                   // OK: A's account, the field holding the balance
        var amount = d.View(s, P3Data, layout)!;
        Assert.Contains(amount.Rows, r => r.Text == UiText.S("p3town.bank_how_much") + " 300_");
        Assert.Empty(amount.Picks);
        Assert.Equal("", Press(d, s, Keys.C));                // CANCEL: back to the picker, nothing taken
        Assert.True(d.Picking);
        Assert.Equal((300, 10), (s.Party.Members[0].GoldInBank, s.Party.GoldCarried));

        Press(d, s, Keys.Down, Keys.O, Keys.Back, Keys.O);     // B, 40 → 4, OK
        Assert.Equal((36, 14), (s.Party.Members[1].GoldInBank, s.Party.GoldCarried));
        Assert.True(d.Picking);                                // back to "Who will withdraw?"
        Assert.Equal("", Press(d, s, Keys.C));                // CANCEL leaves
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void Withdraw_WithoutAmigaKeys_OAndCDoNothing()
    {
        var s = Town();
        s.LeaveGuild();
        s.Party.Members[0].GoldInBank = 300;
        var d = new WithdrawDialog();
        d.Open();
        Assert.Null(Press(d, s, Keys.O, Keys.C));
        Assert.True(d.Picking);
        Press(d, s, Keys.D1);
        Assert.Null(Press(d, s, Keys.O, Keys.C, Keys.Escape)); // DOS's field: no OK / CANCEL / Esc
        Assert.False(d.Picking);
        Assert.Equal(UiText.S("towndlg.type_amount_back_clear_digit_enter"), d.Help);
    }

    [OriginalDataFact]
    public void AmigaShareWindow_BankSharesOnly_PicksAreTheCounts()
    {
        var layout = Windows();
        Assert.NotNull(layout);
        var prompt = new SessionPrompt(SessionPromptKind.BankShares, ["You have: 900 gold", "Shares for: Ann"], ["0", "1", "2", "3"]);
        var view = AmigaShareWindow.View(prompt, 2, layout!)!;
        Assert.Equal(layout!.Bank, view.Window);
        Assert.Equal("  Shares for: Ann", view.Rows[0].Text);
        Assert.Contains(view.Rows, r => r.Text == "You have: 900 gold"); // every prompt line shows
        Assert.Equal([Keys.D0, Keys.D1, Keys.D2, Keys.D3], view.Picks.Select(p => p.Key));
        Assert.Equal(UiText.F("p3town.bank_x_shares", "2"), view.Rows[view.Picks[2].Row].Text);
        Assert.True(view.Rows[view.Picks[2].Row].Highlight);
        Assert.Equal(UiText.F("p3town.bank_one_share", "1"), view.Rows[view.Picks[1].Row].Text);
        Assert.True(view.Picks.Select(p => view.Rows[p.Row].Y).Distinct().Count() == 4); // one row each, none overlapping
        // III's town-entry XP shares keep their own box.
        Assert.Null(AmigaShareWindow.View(prompt with { Kind = SessionPromptKind.ExperienceShares }, 0, layout));
        Assert.Equal(Keys.Enter, AmigaShareWindow.Choice(Keys.O));
        Assert.Null(AmigaShareWindow.Choice(Keys.C));
        Assert.Equal(Keys.D2, AmigaShareWindow.Choice(Keys.D2));
    }

    [OriginalDataFact]
    public void Trade_AmigaWindows_CursorPicks_CancelLeaves()
    {
        var s = Town();
        s.LeaveGuild();
        var d = new TradeDialog { Data = P3Data };
        Assert.Null(d.Open(s));
        var b = s.Party.Members[1];
        b.InventoryItemIds.RemoveAt(b.InventoryItemIds.Count - 1);
        var item = s.Party.Members[0].InventoryItemIds[0];
        if (Windows() is { } layout)
        {
            var who = d.View(s, P3Data, layout)!;
            Assert.Equal(layout.TradeWho, who.Window);
            Assert.Equal(UiText.S("p3town.trade_whose"), who.Rows[0].Text);
            Assert.True(who.Rows[1].Highlight);
        }
        Press(d, s, Keys.Enter);                 // A
        if (Windows() is { } layout2)
            Assert.Equal(layout2.TradeItem, d.View(s, P3Data, layout2)!.Window);
        Press(d, s, Keys.O);                     // the first item
        Press(d, s, Keys.Down, Keys.Enter);      // B gets it
        Assert.Contains(item, b.InventoryItemIds);
        Assert.True(d.IsOpen);                   // back to "Whose item shall be traded?"
        Assert.Equal("", Press(d, s, Keys.C));
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void Training_AgreeAsksThreeSkillPicks_ThenTrains()
    {
        var s = Town();
        var a = s.Party.Members[0];
        a.Experience = 1_000_000;
        s.Party.GoldCarried = 60_000;
        var d = new GuildPartyPickDialog();
        Assert.Null(d.OpenTraining(s));
        Press(d, s, Keys.D1);
        Press(d, s, Keys.A);
        Assert.Equal(1, a.Level); // nothing yet: the picks come first
        Assert.Equal(11, d.Lines(s, P3Data).Count); // counter, header, nine skills
        Press(d, s, Keys.D2);
        Press(d, s, Keys.D2);
        Press(d, s, Keys.Escape);
        Assert.Equal(2, a.Level);
        Assert.True(d.IsOpen); // back at the WHO list
    }
}
