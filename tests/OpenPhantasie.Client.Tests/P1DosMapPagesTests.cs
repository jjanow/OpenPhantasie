using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s154: I DOS's map texts on the path with AMIGA SCREENS off (DOS <c>message-and-text.md</c> "Text boxes, key waits and
/// timed lines"): every centred-band line (<c>0xda83</c>), MESS text window (<c>0x2a95</c>) and damage report is its own page
/// waiting for a key, in log order; message cells stay on the line; nothing is dropped. The defeat's lines hold 5.00 s each,
/// a key ending the current one (OV1 <c>0x24c</c> → COM <c>0x3bf8(500)</c>).
/// </summary>
public partial class P1DosMapPagesTests
{
    private static DosScreens Data() => DosScreens.TryLoad(GameDataRepository.DefaultPathFor(GameId.P1))!;

    private static DosMapBoxes Boxes() => Data().MapBoxes!;

    private static GameSession InDng1(int x = 20, int y = 0, int? spot = null, int? disarm = null)
    {
        var s = ClientSessions.Overworld();
        foreach (var m in s.Party.Members)
        {
            m.HpCur = m.HpMax = 999;
            m.Skills = m.Skills with { SpotTrap = spot ?? m.Skills.SpotTrap, DisarmTrap = disarm ?? m.Skills.DisarmTrap };
        }
        s.EnterDungeon(1, x, y);
        return s;
    }

    [Fact]
    public void Data_HoldsTheBandKeys_AndTheDefeatsFiveSeconds()
    {
        var d = Data();
        Assert.Contains("core.dungeon.that_is_the_wall", d.MapBoxes!.DungeonKeys);
        Assert.Contains("core.dungeon.stairs_from_level_x_to_x", d.MapBoxes.DungeonKeys); // s154 live: the stairs band waits
        Assert.Contains("core.script.monsters_appear", d.MapBoxes.DungeonKeys);
        Assert.Contains("core.encounter.a_night_encounter", d.MapBoxes.Keys);
        Assert.Contains("core.overworld.that_is_the_ocean", d.MapBoxes.Keys);
        Assert.Equal(2, d.MapBoxes.Splits["core.encounter.x_on_watch_hears"].Count);
        Assert.Equal(new DosDefeatTiming(5.0, KeyEndsLine: true), d.Defeat); // 0x3bf8(500): hundredths
        // Every key names a string the table holds (a typo would never match a line).
        foreach (var key in d.MapBoxes.Keys.Concat(d.MapBoxes.DungeonKeys))
            Assert.NotEqual(key, GameText.S(key));
    }

    [Fact]
    public void Data_RejectsBadValues()
    {
        Assert.Throws<InvalidDataException>(() => DosScreens.Parse("""{ "defeat": { "line_hundredths": 0 } }"""));
        Assert.Throws<InvalidDataException>(() => DosScreens.Parse(
            """{ "map_boxes": { "keys": ["a"], "splits": { "b": ["x", "y"] } } }"""));
        Assert.Null(DosScreens.Parse("{}").Defeat);
    }

    [OriginalDataFact]
    public void TheWall_IsABandPage()
    {
        var s = InDng1();
        var mark = s.LogSequence;
        s.MoveDungeon(CardinalDirection.West); // (19, 0) is rock
        var pages = P1DosMapPages.ForStep(Boxes(), s, mark)!;
        var page = Assert.Single(pages.Pages);
        Assert.Equal([GameText.S("core.dungeon.that_is_the_wall")], page.Rows);
        Assert.Equal(LogWindowKind.Box, page.Kind);
        Assert.Null(pages.BarLine);
    }

    [OriginalDataFact]
    public void AFailedDisarm_TheBand_TheText_TheReport()
    {
        var s = InDng1(8, 4, spot: 100, disarm: -1);
        s.MoveDungeon(CardinalDirection.North);
        var mark = s.LogSequence;
        s.AnswerDungeonTrap(s.DungeonTrapPrompt!.Disarmers[0]);
        var pages = P1DosMapPages.ForStep(Boxes(), s, mark)!;
        Assert.Equal([LogWindowKind.Box, LogWindowKind.Text, LogWindowKind.Report], pages.Pages.Select(p => p.Kind));
        Assert.EndsWith("FAILS", pages.Pages[0].Rows[0]);
        AllShown(s, mark, pages);
    }

    [OriginalDataFact]
    public void OpTwelve_EachReportAndBandIsItsOwnPage()
    {
        var s = ClientSessions.Overworld();
        s.EnterDungeon(2, 6, 28);
        s.MoveDungeon(CardinalDirection.South);
        var mark = s.LogSequence;
        s.AnswerDungeonEvent(1); // BRIBE THEM: A SPELL IS CAST, the report, THE PARTY IS MOVED
        var pages = P1DosMapPages.ForStep(Boxes(), s, mark)!;
        Assert.Equal(["A SPELL IS CAST"], pages.Pages[0].Rows);
        Assert.Equal(GameText.S("core.script.report"), pages.Pages[1].Rows[0]);
        Assert.Equal(["THE PARTY IS MOVED"], pages.Pages[^1].Rows);
        AllShown(s, mark, pages);
    }

    [OriginalDataFact]
    public void OpTwelvesBand_ShowsThePartyOnTheEventCell_UntilItsKey()
    {
        // s156: DOS draws "THE PARTY IS MOVED" before the move (s58 live: the old row-24 line under it, the map unmoved).
        var s = ClientSessions.Overworld();
        s.EnterDungeon(2, 6, 28);
        s.MoveDungeon(CardinalDirection.South);
        var (level, x, y) = (s.Dungeon!.LevelId, s.Dungeon.X, s.Dungeon.Y);
        var mark = s.LogSequence;
        var moveMark = s.DungeonMoveSequence;
        s.AnswerDungeonEvent(1); // BRIBE THEM: A SPELL IS CAST, the report, THE PARTY IS MOVED, then op 12's move
        Assert.NotEqual((x, y), (s.Dungeon.X, s.Dungeon.Y));
        var pages = P1DosMapPages.ForStep(Boxes(), s, mark, moveMark)!;
        Assert.Equal(["THE PARTY IS MOVED"], pages.Pages[^1].Rows);
        Assert.Equal(new P1MapMessageRun.PartyCell(level, x, y), pages.Pages[^1].PartyAt);
        Assert.All(pages.Pages, p => Assert.NotNull(p.PartyAt)); // every page of this step comes before the move
    }

    [OriginalDataFact]
    public void AMessageCell_StaysOnTheLine_ADescriptionCellIsAPage()
    {
        var s = InDng1();
        var mark = s.LogSequence;
        s.Dungeon!.Level!.Set(20, 1, 0x8b); // 0x23c0: row 24, no wait
        s.MoveDungeon(CardinalDirection.South);
        Assert.Null(P1DosMapPages.ForStep(Boxes(), s, mark));

        mark = s.LogSequence;
        s.Dungeon.Level.Set(20, 2, 0x17); // 0x298b → 0x2a95: the MESS window, waits
        s.MoveDungeon(CardinalDirection.South);
        var page = Assert.Single(P1DosMapPages.ForStep(Boxes(), s, mark)!.Pages);
        Assert.Equal(LogWindowKind.Text, page.Kind);
        Assert.Equal(2, page.Rows.Count);
    }

    [Fact]
    public void ARowTwentyFourLine_BeforeABand_StaysOnTheBar()
    {
        string[] lines = ["A DARK PASSAGE", GameText.S("core.dungeon.that_is_the_wall")];
        var pages = P1DosMapPages.Plan(Boxes(), lines, [], dungeon: true)!;
        Assert.Equal("A DARK PASSAGE", pages.BarLine);
        Assert.DoesNotContain("A DARK PASSAGE", pages.PagedLines);
        Assert.Contains(lines[1], pages.PagedLines);
    }

    [Fact]
    public void EveryLinePaged_KeepsTheRowTwentyFourLineFromBefore()
    {
        // s156: DOS's boxes restore the screen under them; row 24 changes only for a new row-24 line.
        var paged = P1DosMapPages.Plan(Boxes(), [GameText.S("core.dungeon.that_is_the_wall")], [], dungeon: true)!;
        Assert.Null(paged.BarLine);
        Assert.Equal("A DARK PASSAGE", paged.MessageLine("A DARK PASSAGE"));
        Assert.Equal("", paged.MessageLine(null));
        var mixed = P1DosMapPages.Plan(Boxes(), ["A COLD DRAFT", GameText.S("core.dungeon.that_is_the_wall")], [], dungeon: true)!;
        Assert.Equal("A COLD DRAFT", mixed.MessageLine("A DARK PASSAGE"));
    }

    [OriginalDataFact]
    public void TheWatchAndHears_IsOneTwoRowBand_AndAMenuTitleIsNoBand()
    {
        var both = GameText.F("core.encounter.x_on_watch_hears", "ARENTINO");
        var pages = P1DosMapPages.Plan(Boxes(), [both], [], dungeon: false)!;
        Assert.Equal([GameText.F("core.encounter.x_on_watch", "ARENTINO"), GameText.S("core.encounter.and_hears_the_monsters")],
            Assert.Single(pages.Pages).Rows);
        var title = GameText.F("core.encounter.x_hears_monsters", "ARENTINO");
        Assert.Null(P1DosMapPages.Plan(Boxes(), [title], [], dungeon: false, skip: title));
        // The overworld's keys are not the dungeon's.
        Assert.Null(P1DosMapPages.Plan(Boxes(), [GameText.S("core.dungeon.that_is_the_wall")], [], dungeon: false));
    }

    [Fact]
    public void ALocationsPages_AreOneBandPage()
    {
        string[] lines = ["YOU ARE ON A ROAD.", "TO THE NORTH IS A TINY HAMLET"];
        var windows = new[] { new LogWindow(0, 2, LogWindowKind.Box) { Rows = ["  YOU ARE ON A ROAD.  ", "TO THE NORTH IS A TINY HAMLET"] } };
        var pages = P1DosMapPages.Plan(Boxes(), lines, windows, dungeon: false)!;
        Assert.Equal(["YOU ARE ON A ROAD.", "TO THE NORTH IS A TINY HAMLET"], Assert.Single(pages.Pages).Rows);
        Assert.Contains("YOU ARE ON A ROAD. TO THE NORTH IS A TINY HAMLET", pages.PagedLines); // the joined sign line
    }

    [Fact]
    public void LeavingTown_IsDosLeavingPercentS()
    {
        // OV1 0x483e: "LEAVING %s" (OV1 string 0xc284) with the TWNS record's name.
        Assert.Equal("LEAVING PELNOR", UiText.F("game.dos_leaving_x", "PELNOR"));
    }

    [Fact]
    public void DefeatClock_FiveSecondsALine_AKeyEndsTheCurrentOne()
    {
        var clock = new P1DosDefeatClock(SessionPromptKind.DefeatOutcome, 4, new DosDefeatTiming(5.0, true), 10.0);
        Assert.Equal(1, clock.LinesShown(10.0, 4));
        Assert.Equal(1, clock.LinesShown(14.99, 4));
        Assert.Equal(2, clock.LinesShown(15.0, 4));
        clock.Key(16.0); // line 2 ends at once; line 3 starts at 16 s
        Assert.Equal(3, clock.LinesShown(16.0, 4));
        Assert.Equal(3, clock.LinesShown(20.99, 4));
        Assert.Equal(4, clock.LinesShown(21.0, 4));
        Assert.False(clock.Done(25.99));
        Assert.True(clock.Done(26.0)); // 5 s after the last line starts: answered by itself
    }

    [Fact]
    public void DefeatClock_WithoutKeyEndsLine_IgnoresKeys()
    {
        var clock = new P1DosDefeatClock(SessionPromptKind.PartyDefeat, 1, new DosDefeatTiming(5.0, false), 0);
        clock.Key(1);
        Assert.False(clock.Done(4.99));
        Assert.True(clock.Done(5.0));
    }

    // --- s156: the pages at DOS's places (dos-screens.json pages; DOSBox-X s154 / s156, OV2 0xb9d1 calls) ---

    private static DosPagePlaces Places() => Data().Pages!;

    [Fact]
    public void Places_AreDosRowsAndTheFiveReportWindows()
    {
        var p = Places();
        Assert.Equal(9, p.BandTopRow);
        Assert.Equal(4, p.TextTopRow);
        Assert.Equal(new DosReportWindow(6, 34, 7, 16, (16, 8), (7, 10)), p.Reports["core.script.report"]); // OV2 0x25d1
        Assert.Equal(new DosReportWindow(8, 29, 7, 16, (12, 8), (9, 10)), p.Reports["core.pool.a_magical_pool"]); // 0x658d
        Assert.Equal(new DosReportWindow(5, 32, 7, 16, (13, 8), (6, 10)), p.Reports["core.inn.in_the_inn"]); // 0x6749, s45 shot
        Assert.Equal(5, p.Reports.Count);
    }

    [OriginalDataFact]
    public void TheWallBand_IsRowsNineToEleven_TheTextCentredAtColumnTwelve()
    {
        var page = Assert.Single(P1DosMapPages.Plan(Boxes(), [GameText.S("core.dungeon.that_is_the_wall")], [], dungeon: true)!.Pages);
        var layout = P1DosPageLayout.For(Places(), page)!;
        Assert.False(layout.Framed);
        Assert.Equal((0, 39, 9, 11), (layout.Left, layout.Right, layout.Top, layout.Bottom)); // s154 shot: rules y 73-74 / 93-94
        Assert.Equal(new P1DosPageLayout.Cell(12, 10, "THAT IS THE WALL"), Assert.Single(layout.Texts)); // x 96 live
    }

    [OriginalDataFact]
    public void ATwoRowBand_EndsAtRowTwelve()
    {
        var both = GameText.F("core.encounter.x_on_watch_hears", "ARENTINO");
        var layout = P1DosPageLayout.For(Places(), Assert.Single(P1DosMapPages.Plan(Boxes(), [both], [], dungeon: false)!.Pages))!;
        Assert.Equal(12, layout.Bottom);
        Assert.Equal([10, 11], layout.Texts.Select(t => t.Row));
    }

    [Fact]
    public void ASign_IsCentredAsStored_ItsPaddingCounted()
    {
        // s156, s45 DOSBox shot of the Red Pony sign: rules at y 73-74 / 101-102, the rows from columns 2 and 9 (the stored
        // 39-character rows centred whole), not the trimmed text's (4 and 11).
        string[] lines = ["TO THE SOUTH STANDS A HALL NAMED", "'THE OLD MULE INN'"];
        var windows = new[]
        {
            new LogWindow(0, 2, LogWindowKind.Box)
                { Rows = ["  TO THE SOUTH STANDS A HALL NAMED     ", "         'THE OLD MULE INN'            "] },
        };
        var page = Assert.Single(P1DosMapPages.Plan(Boxes(), lines, windows, dungeon: false)!.Pages);
        var layout = P1DosPageLayout.For(Places(), page)!;
        Assert.Equal(12, layout.Bottom);
        Assert.Equal([2, 9], layout.Texts.Select(t => t.Column + t.Text.Length - t.Text.TrimStart().Length));
    }

    [Fact]
    public void AMessText_IsFromRowFour_TheRecordsFromColumnZero()
    {
        // s156 DOSBox-X (DNG9's gate, three records): rules at y 33-34 and 69-70 = rows 4 and 8.
        var page = new P1DosMapPages.Page(["THE PARTY MEMBERS SENSE A GREAT DARKNESS", "ALL AROUND", "STRENGTH TO GO ON..."], LogWindowKind.Text);
        var layout = P1DosPageLayout.For(Places(), page)!;
        Assert.Equal((4, 8), (layout.Top, layout.Bottom));
        Assert.All(layout.Texts, t => Assert.Equal(0, t.Column));
        Assert.Equal([5, 6, 7], layout.Texts.Select(t => t.Row));
    }

    [OriginalDataFact]
    public void TheOpReport_IsFramedAtDosPlaces()
    {
        var s = ClientSessions.Overworld();
        s.EnterDungeon(2, 6, 28);
        s.MoveDungeon(CardinalDirection.South);
        var mark = s.LogSequence;
        s.AnswerDungeonEvent(1); // BRIBE THEM
        var report = P1DosMapPages.ForStep(Boxes(), s, mark)!.Pages[1];
        var layout = P1DosPageLayout.For(Places(), report)!;
        Assert.True(layout.Framed);
        Assert.Equal((6, 34, 7, 16), (layout.Left, layout.Right, layout.Top, layout.Bottom));
        Assert.Equal(new P1DosPageLayout.Cell(16, 8, "REPORT:"), layout.Texts[0]);
        Assert.Equal((7, 10), (layout.Texts[1].Column, layout.Texts[1].Row));
        Assert.Equal(report.Rows.Count, layout.Texts.Count); // every row placed
    }

    [OriginalDataFact]
    public void TheWildernessInnAndThePool_AreTheirOwnDosWindows()
    {
        // The inn: "AN INN. ENTER?" YES (the Client's AnswerPrompt plans the pages after it), OV2 0x6749's window.
        var s = ClientSessions.Overworld();
        s.Party.GoldCarried = 300;
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 6, 13);
        s.MoveOverworld(CardinalDirection.East); // onto the inn (tile 41)
        var mark = s.LogSequence;
        Assert.Equal(TownTransactionKind.Ok, s.StayAtWildernessInn().Kind);
        var inn = Assert.Single(P1DosMapPages.ForStep(Boxes(), s, mark)!.Pages);
        var layout = P1DosPageLayout.For(Places(), inn)!;
        Assert.Equal((5, 32, 7, 16), (layout.Left, layout.Right, layout.Top, layout.Bottom));
        Assert.Equal(new P1DosPageLayout.Cell(13, 8, GameText.S("core.inn.in_the_inn")), layout.Texts[0]); // s45 shot: x 104
        Assert.Equal((6, 10), (layout.Texts[1].Column, layout.Texts[1].Row)); // s45 shot: x 48, row 10

        // The pool: OV2 0x658d's window, the rows from (9, 10).
        mark = s.LogSequence;
        s.Campaign.OnSpecialSite(s, new OverworldMoveResult { Kind = OverworldMoveKind.SpecialSite, Tile = 111 });
        var pool = Assert.Single(P1DosMapPages.ForStep(Boxes(), s, mark)!.Pages);
        layout = P1DosPageLayout.For(Places(), pool)!;
        Assert.Equal((8, 29, 7, 16), (layout.Left, layout.Right, layout.Top, layout.Bottom));
        Assert.Equal((12, 8), (layout.Texts[0].Column, layout.Texts[0].Row));
        Assert.All(layout.Texts.Skip(1), t => Assert.Equal(9, t.Column));
    }

    [Fact]
    public void WhatDosCantHoldWhole_FallsBackToTheScrollingPanel()
    {
        var tooLong = new P1DosMapPages.Page([.. Enumerable.Range(0, 20).Select(i => "ROW " + i)], LogWindowKind.Text);
        Assert.Null(P1DosPageLayout.For(Places(), tooLong)); // past row 24: the remaster's panel scrolls it
        var wide = new P1DosMapPages.Page([new string('X', 41)], LogWindowKind.Box);
        Assert.Null(P1DosPageLayout.For(Places(), wide));
        var unknown = new P1DosMapPages.Page(["SOMETHING:", "A ROW"], LogWindowKind.Report);
        Assert.Null(P1DosPageLayout.For(Places(), unknown));
        var crowded = new P1DosMapPages.Page([GameText.S("core.inn.in_the_inn"), .. Enumerable.Range(0, 7).Select(i => "M" + i)],
            LogWindowKind.Report);
        Assert.Null(P1DosPageLayout.For(Places(), crowded)); // rows 10..16 would cross the frame on row 16
    }

    private static void AllShown(GameSession s, long mark, P1DosMapPages pages)
    {
        foreach (var line in s.LogSince(mark).Select(l => l.Trim()).Where(l => l.Length > 0))
            Assert.True(pages.PagedLines.Contains(line) || pages.BarLine == line, $"lost: {line}");
    }
}

/// <summary>
/// s154: the padded report rows (Core's <c>%-10.9s</c> / <c>%-3d</c>, both paths) still fit I Amiga's requester 21 (the
/// Amiga path's report window draws Core's lines on its member rows, never cut).
/// </summary>
public class P1PaddedReportFitTests
{
    [OriginalDataFact]
    public void TheLongestPaddedRow_FitsRequesterTwentyOne()
    {
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.MapMessage!;
        var window = layout.Window!;
        var font = FontSources.Named(layout.Font, layout.FontFile);
        Assert.NotNull(font); // z6font ships with the data (graphics/fonts)
        string[] rows =
        [
            GameText.F("core.dungeon.x_sinks_and_takes_x_damage", "ARENTINOS", 999),
            GameText.F("core.script.report_x_takes_x_damage", "ARENTINOS", 999),
            GameText.F("core.dungeon.trap_x_takes_x_damage", "ARENTINOS", 999),
            GameText.F("core.dungeon.x_avoids_the_trap", "ARENTINOS"),
        ];
        foreach (var row in rows)
            Assert.True(window.RowX + PixelFont.Measure(row, font) <= window.Box.X + window.Box.W,
                $"{row}: {PixelFont.Measure(row, font)}");
    }
}
