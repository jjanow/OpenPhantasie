using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The Guild treasury in the Client (P3 QoL, <c>docs/plan/11-guild-treasury.md</c> sections 5 and 6): the Options QUALITY OF
/// LIFE section, the Treasury window, the gold line and the sheets without per-character gold; off = as before.
/// </summary>
public class TreasuryClientTests
{
    private static readonly GameDataRepository Data = ClientSessions.Data;

    /// <summary>A new I game (treasury <paramref name="treasury"/>) with a two-member party, in Pelnor's town menu.</summary>
    private static GameSession Town(string treasury)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1).With("treasury", treasury));
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("Alpha", CharacterClass.Fighter), ("Bravo", CharacterClass.Priest) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out _));
        s.LeaveGuild();
        return s;
    }

    /// <summary>The same party on the overworld carrying <paramref name="purse"/> gold won on this trip.</summary>
    private static GameSession Field(string treasury, long purse)
    {
        var s = Town(treasury);
        Assert.True(s.LeaveTown());
        s.Party.GoldCarried = purse;
        return s;
    }

    // ---- Options: the QUALITY OF LIFE section ------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Options_QolRules_SitUnderTheirOwnHeader_BetweenTheRulesAndOtherGames(bool locked)
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], g => g == GameId.P1);
        o.Open(locked ? Town("off") : null);
        if (!locked)
            o.HandleKeys([Keys.Right]); // tab I (a running game opens on its own tab)
        var rows = o.RowTexts().Select(t => t.Trim()).ToList();
        var rulesHeader = rows.FindIndex(t => t == UiText.S(locked ? "options.rules_locked" : "options.rules_new_games"));
        var qol = rows.IndexOf(UiText.S("options.quality_of_life"));
        var others = rows.IndexOf(UiText.S("options.from_other_games"));
        Assert.True(rulesHeader >= 0 && rulesHeader < qol && qol < others, string.Join(" | ", rows));
        Assert.StartsWith(UiText.S("options.preset"), rows[rulesHeader + 1]); // the preset stays at the top of the rules

        int RowOf(RuleOptionDescriptor d) => rows.FindIndex(t => t.StartsWith(UiText.S("option." + d.Id), StringComparison.Ordinal));
        var p1 = RuleOptionRegistry.For(GameId.P1);
        Assert.Contains(p1, d => d.Qol);
        foreach (var d in p1)
        {
            var at = RowOf(d);
            Assert.True(d.Qol ? at > qol && at < others : at > rulesHeader && at < qol, d.Id);
        }
        Assert.True(RowOf(RuleOptionRegistry.Treasury) > qol);
    }

    // ---- the Treasury window -----------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void TreasuryWindow_ShowsTheReport_ScrollsALongLedger_AnyOtherKeyCloses()
    {
        var s = Town("trip");
        for (var i = 0; i < 30; i++)
            s.Treasury.Add(10 + i, TreasuryEntryKind.Sale);
        var report = TreasuryReport.Lines(s.Treasury, 0);
        Assert.True(report.Count > TreasuryDialog.PageRows + 1);

        var d = new TreasuryDialog();
        d.Open();
        var lines = d.Lines(s, Data);
        Assert.Equal(report.Take(TreasuryDialog.PageRows + 1), lines); // the title, then the first page
        Assert.Equal(new ListPage(1, TreasuryDialog.PageRows, 0, report.Count - 1), d.Page);

        Assert.Null(d.HandleKeys([Keys.Down], s)); // scrolls, stays open
        Assert.Equal(report[2], d.Lines(s, Data)[1]);
        d.HandleKeys([Keys.End], s);
        Assert.Equal(report[^1], d.Lines(s, Data)[^1]); // every line reachable
        d.HandleKeys([Keys.Home], s);
        Assert.Equal(report[1], d.Lines(s, Data)[1]);
        Assert.True(d.IsOpen);

        Assert.Equal("", d.HandleKeys([Keys.Space], s));
        Assert.False(d.IsOpen);
        d.Open();
        Assert.Equal("", d.HandleKeys([Keys.Escape], s));
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void TreasuryWindow_ShortLedger_HasNoPage()
    {
        var s = Town("trip");
        var d = new TreasuryDialog();
        d.Open();
        Assert.Equal(TreasuryReport.Lines(s.Treasury, 0), d.Lines(s, Data));
        Assert.Null(d.Page);
    }

    [OriginalDataFact]
    public void TreasuryWindow_III_AmigaWindow9_TitleAndAPageOfTheReport()
    {
        var p3 = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
        var windows = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!;
        var s = GameSessionFactory.Create(p3, new SeededDosRandomSource(4));
        s.StartNewGame(GameOptions.ForPreset(GameId.P3).With("treasury", "trip"));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("A", Race.Human, CharacterClass.Fighter), out _));
        s.LeaveGuild();
        for (var i = 0; i < 20; i++)
            s.Treasury.Add(5, TreasuryEntryKind.Sale);
        var d = new TreasuryDialog();
        d.Open();
        var view = d.View(s, p3, windows)!;
        var report = TreasuryReport.Lines(s.Treasury, 0);
        Assert.Equal(report.Take(TreasuryDialog.PageRows + 1), view.Rows.Select(r => r.Text));
        Assert.Empty(view.Picks);
        Assert.All(view.Rows, r => Assert.True(r.Y + 10 < 200)); // no row below the screen
        d.HandleKeys([Keys.End], s);
        Assert.Equal(report[^1], d.View(s, p3, windows)!.Rows[^1].Text);
    }

    // ---- the gold line -----------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void GoldLine_InTheField_SpendableAndTrip_InTownNoTrip()
    {
        var town = Town("trip");
        Assert.Equal(TreasuryReport.GoldLine(town.SpendableGold, 0), UiOverlays.TreasuryGoldLine(town));
        var field = Field("trip", 120);
        Assert.Equal(TreasuryReport.GoldLine(field.SpendableGold, 120), UiOverlays.TreasuryGoldLine(field));
        Assert.Contains("120", UiOverlays.TreasuryGoldLine(field));
        Assert.Null(UiOverlays.TreasuryGoldLine(Field("off", 120)));
    }

    [OriginalDataFact]
    public void StatusLine_UnderTheTreasury_CarriesTheGoldLine_AndFitsTheBand()
    {
        var s = Field("trip", 120);
        var status = UiOverlays.StatusText(s, new ClientSettings(), "OUT1 (12,34) DAY 3");
        Assert.Contains(UiOverlays.TreasuryGoldLine(s)!, status);
        Assert.True(PixelFont.Measure(status) <= UiOverlays.StatusWidth, status);

        // A long line leaves out the mode word, then the trip part (the sidebar keeps the whole gold line); never past the band.
        s.Treasury.Add(1_234_567, TreasuryEntryKind.Sale);
        s.Party.GoldCarried = 98_765;
        var longer = UiOverlays.StatusText(s, new ClientSettings(), "OUT12 (123,45) DAY 120");
        Assert.True(PixelFont.Measure(longer) <= UiOverlays.StatusWidth, longer);
        Assert.Contains(UiText.F("core.treasury.gold_x", s.SpendableGold), longer);
    }

    [OriginalDataFact]
    public void StatusLine_TreasuryOff_IsUnchanged()
    {
        var s = Field("off", 120);
        var settings = new ClientSettings();
        Assert.Equal(UiText.F("ui.x_gold_x_party_x_x", "OVERWORLD", 120, 2, 2, "OUT"),
            UiOverlays.StatusText(s, settings, "OUT"));
        Assert.Equal(UiText.F("ui.x_gold_x_party_x_x_2", "OVERWORLD", 120, 2, 2, settings.IntegerScale),
            UiOverlays.StatusText(s, settings, ""));
    }

    [OriginalDataFact]
    public void GoldFitting_WholeLine_ThenWithoutTheTrip_ThenNone_AndNoneWhenOff()
    {
        var on = Field("trip", 120);
        var full = UiOverlays.TreasuryGoldLine(on)!;
        Assert.Contains("120", full);
        Assert.Equal(full, UiOverlays.TreasuryGoldFitting(on, 320));
        // A bar's free end too narrow for the trip part: the spendable total alone (the field bars, III's town bar).
        var plain = UiText.F("core.treasury.gold_x", on.SpendableGold);
        Assert.Equal(plain, UiOverlays.TreasuryGoldFitting(on, PixelFont.Measure(plain)));
        Assert.Null(UiOverlays.TreasuryGoldFitting(on, PixelFont.Measure(plain) - 1));
        Assert.Null(UiOverlays.TreasuryGoldFitting(Field("off", 120), 320));
    }

    [OriginalDataFact]
    public void ShownGold_IsSpendableUnderTheTreasury_ThePurseWithout()
    {
        var on = Town("trip");
        on.Treasury.Add(500, TreasuryEntryKind.Sale);
        Assert.Equal(on.SpendableGold, UiOverlays.ShownGold(on));
        Assert.True(UiOverlays.ShownGold(on) >= 500);
        var off = Town("off");
        off.Party.GoldCarried = 77;
        Assert.Equal(77, UiOverlays.ShownGold(off));
        Assert.Contains(UiText.F("inspect.gold_pieces_x", on.SpendableGold), InspectDialog.SummaryLines(on));
    }

    // ---- no per-character gold ---------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void InspectSheet_UnderTheTreasury_HasNoBankGold()
    {
        var ch = new Character { Name = "X", Stats = new CharacterStats(10, 10, 10, 12, 10, 10), GoldInBank = 345 };
        var off = InspectDialog.SheetLines(ch);
        Assert.Contains(UiText.F("inspect.constitution_x_gold_in_bank_x", 12, 345), off);
        var on = InspectDialog.SheetLines(ch, treasury: true);
        Assert.Contains(UiText.F("inspect.constitution_x", 12), on);
        Assert.DoesNotContain(on, l => l.Contains("GOLD IN BANK", StringComparison.Ordinal));
        Assert.Equal(off.Count, on.Count);
    }

    [OriginalDataFact]
    public void P1AmigaSheet_UnderTheTreasury_LeavesTheBankRowEmpty()
    {
        var sheet = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.MapRequesters!.Sheet!;
        var ch = new Character { Name = "X", Stats = new CharacterStats(1, 1, 1, 1, 1, 1), GoldInBank = 345 };
        Assert.Contains(P1MapRequesters.Sheet(sheet, ch).Ops, o => o.Text == UiText.F(P1MapRequesters.BankRowId, 345));
        var on = P1MapRequesters.Sheet(sheet, ch, treasury: true);
        Assert.DoesNotContain(on.Ops, o => o.Text is { } t && t.Contains("BANK", StringComparison.Ordinal));
        Assert.DoesNotContain(on.Ops, o => o.Text is { } t && t.Contains("345", StringComparison.Ordinal));
    }
}
