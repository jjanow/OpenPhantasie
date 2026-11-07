using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s154: the remaining requester-21 reports drawn as I Amiga's (FORMAT.md "Requester 21 on the map"): the magical pool and
/// the wilderness inn (Core's Report windows), the map's Health (h50 <c>+0x420</c>) and the fight's Party health (h44
/// <c>+0x1478</c>, Top 23), the Amiga's titles with their leading blanks, and Party Options › Show names as requester 23
/// (h44 <c>+0x138c</c>: rows from the top, not centred, ⌊(h3 + 360) / 60⌋ s).
/// </summary>
public class P1ReportWindowTests
{
    private static GraphicsPack Pack() => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static MapMessageWindow Window() => Pack().MapMessage!.Window!;

    [OriginalDataFact]
    public void Titles_AreTheAmigas_WithTheirLeadingBlanks()
    {
        var w = Window();
        Assert.Equal(["  SWIMMING:", "x"], P1MapReports.Report(w, [GameText.S("core.dungeon.swimming"), "x"]));
        Assert.Equal("   REPORT:", P1MapReports.Report(w, [GameText.S("core.script.report")])[0]); // h42 +0x1a2
        Assert.Equal("  IN THE INN", P1MapReports.Report(w, [GameText.S("core.inn.in_the_inn")])[0]); // h72 +0xdf
        Assert.Equal("A MAGICAL POOL", P1MapReports.Report(w, [GameText.S("core.pool.a_magical_pool")])[0]);
        Assert.Equal("IN THE TRAP:", P1MapReports.Report(w, [GameText.S("core.dungeon.in_the_trap")])[0]);
        Assert.Equal(["NOT A TITLE", "row"], P1MapReports.Report(w, ["NOT A TITLE", "row"])); // left as logged
    }

    /// <summary>s156: the pool's rows in the Amiga's text (h72 <c>+0xb4</c> "%-10.9s IS CHANGED": a blank more than Core's).</summary>
    [OriginalDataFact]
    public void ThePoolsRows_AreTheAmigas()
    {
        var w = Window();
        var core = GameText.F("core.pool.x_is_changed", "ALDO");
        Assert.Equal("ALDO      IS CHANGED", core);
        Assert.Equal(["A MAGICAL POOL", "ALDO       IS CHANGED"],
            P1MapReports.Report(w, [GameText.S("core.pool.a_magical_pool"), core]));
        Assert.Equal("NINELETTR  IS CHANGED", P1MapReports.Row(w, GameText.F("core.pool.x_is_changed", "NINELETTR")));
        Assert.Equal("ALDO      IS RESTING", P1MapReports.Row(w, "ALDO      IS RESTING")); // not listed: as logged
    }

    [OriginalDataFact]
    public void TheInnsReport_ShowsItsRows_NotTheJoinedLine()
    {
        var layout = Pack().MapMessage!;
        string[] rows = ["IN THE INN:", "ALDO      IS RESTING"];
        var joined = string.Join("; ", rows);
        var run = P1MapMessageRun.Plan(layout, [joined], 0, null,
            windows: [new LogWindow(0, 1, LogWindowKind.Report) { Rows = rows }])!;
        Assert.True(run.Waits);
        Assert.Equal(rows, run.WaitLines);
        Assert.Contains(joined, run.BoxedLines); // the band does not repeat it
    }

    [OriginalDataFact]
    public void ThePoolsReport_WaitsWithItsLogLines()
    {
        var layout = Pack().MapMessage!;
        string[] lines = ["A MAGICAL POOL", "ALDO      IS CHANGED"];
        var run = P1MapMessageRun.Plan(layout, lines, 0, null, windows: [new LogWindow(0, 2, LogWindowKind.Report)])!;
        Assert.Equal(lines, run.WaitLines);
    }

    [OriginalDataFact]
    public void MapHealth_IsTheAmigasRows()
    {
        var s = ClientSessions.Overworld();
        var m = s.Party.Members[0];
        var lines = P1MapReports.MapHealth(Window(), s.Party)!;
        Assert.Equal("HEALTH REPORT:", lines[0]); // h51 +0x75
        Assert.Equal(s.Party.Members.Count + 1, lines.Count);
        var name = m.Name.Length > 9 ? m.Name[..9] : m.Name;
        // h51 +0x8b " %-10.9s%-6.5sHP%4d/%-4dMP%4d/%-3d ": a blank first, status DEAD / OK / SLEEP (h12 +0xb78); the
        // remaster prints the manual's "OKAY" (D-064, s155).
        Assert.Equal($" {name,-10}{"OKAY",-6}HP{m.HpCur,4}/{m.HpMax,-4}MP{m.MagicCur,4}/{m.MagicMax,-3} ", lines[1]);
    }

    [OriginalDataFact]
    public void FightHealth_IsRequesterTwentyOne_AtTheMapsTop()
    {
        var w = Window();
        // s158 live (p1-amiga-combat-party-health-before-orders): Top 45 as on the map; h71 +0x106's Top 23 is the orders'.
        Assert.Equal(45, w.FightTop);
        var moved = w.MovedTo(w.FightTop!.Value);
        Assert.Equal(new Box(33, 45, 255, 110), moved.Box);
        Assert.Equal(w.Box, moved.Box);
        Assert.Equal(Pack().CombatField!.Orders!.Box with { Y = 45 }, moved.Box); // the orders requester is the same requester 21
        Assert.Equal((w.RowY, w.TitleY, w.OkTextY), (moved.RowY, moved.TitleY, moved.OkTextY));
        var s = ClientSessions.Overworld();
        var m = s.Party.Members[0];
        var lines = P1MapReports.FightHealth(w, s)!;
        Assert.Equal("HEALTH REPORT:", lines[0]); // h45 +0x107
        var name = m.Name.Length > 9 ? m.Name[..9] : m.Name;
        // h45 +0x116 "%-10.9s%c HP%4d/%-4dA:%-3.2dMP%4d/%-3d ": no blank before MP.
        Assert.Equal($"{name,-10}  HP{m.HpCur,4}/{m.HpMax,-4}A:{m.Skills.Attack / 75 + 1,-3}MP{m.MagicCur,4}/{m.MagicMax,-3} ",
            lines[1]);
    }

    [OriginalDataFact]
    public void ShowNames_IsSixSecondsAtNormal_SevenAtSlow()
    {
        var field = Pack().CombatField!;
        var message = field.Message!;
        Assert.Equal(360, message.ListTicks); // h44 +0x1468: h3 + 360
        Assert.Equal(6, message.ListSeconds(field.LinePolls(0)));
        Assert.Equal(6, message.ListSeconds(field.LinePolls(2)));
        Assert.Equal(7, message.ListSeconds(field.LinePolls(3)));
        Assert.Equal(3, message.ClockCut(message.ListSeconds(field.LinePolls(2)), 13)); // the 8-s wrap cuts it
    }

    [OriginalDataFact]
    public void Leaving_IsPenFourAtOneHundredSix()
    {
        var town = Pack().TownScreen!;
        Assert.Equal("#EEEE00", town.LeavingColour); // pen 4 of 0x1947C (shot p1-amiga-town-leaving-pelnor)
        Assert.Equal(0.033, town.LeavingSeconds); // s154 film: two 60-Hz frames
        var line = UiOverlays.TownLeavingLine(town, "PELNOR");
        Assert.Equal("LEAVING PELNOR    ", line); // the name's 10-character field (h29 +0x168, the record's +280)
        using (PixelFont.With(UiOverlays.TownTextFont(town)))
            Assert.Equal(106, UiOverlays.TownLeavingLeft(line)); // (320 - 6 * 18) / 2
    }

    [OriginalDataFact]
    public void Leaving_ThenTheFilmedBlack_ThenTheMap()
    {
        var town = Pack().TownScreen!;
        Assert.Equal(2.24, town.LeavingBlackSeconds); // s154 film: 2.35 / 2.14 s of black before the overworld (mean)
        Assert.Equal(LeavingPhase.Line, town.LeavingAt(0));
        Assert.Equal(LeavingPhase.Black, town.LeavingAt(0.033));
        Assert.Equal(LeavingPhase.Black, town.LeavingAt(2.27));
        Assert.Equal(LeavingPhase.Map, town.LeavingAt(2.28));
    }

    [OriginalDataFact]
    public void TheTitle_LoadsTopaz()
    {
        var w = Window();
        Assert.True(File.Exists(w.TitleFontFile)); // s154: the id alone left the title in the remaster's 5x7
        var topaz = FontSources.Named(w.TitleFont, w.TitleFontFile)!;
        Assert.Equal(8, topaz.Height);
        Assert.Equal(14 * 8, PixelFont.Measure("HEALTH REPORT:", topaz));
    }

    [OriginalDataFact]
    public void TheInnStay_PlaysAsAReport_OnTheOverworld()
    {
        var s = ClientSessions.Overworld();
        s.Party.GoldCarried = 300;
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 6, 13);
        s.MoveOverworld(CardinalDirection.East); // onto the inn, tile 41
        var mark = s.LogSequence; // as Game1.AnswerPrompt takes it before the answer
        var r = s.StayAtWildernessInn();
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        var run = P1MapMessageRun.ForStep(Pack().MapMessage!, s, mark, 10, null)!;
        Assert.Empty(run.Boxes);
        Assert.True(run.Waits);
        Assert.Equal(r.Lines, run.WaitLines);
        Assert.Contains(r.Message, run.BoxedLines); // the joined line leaves the band
        Assert.Equal("  IN THE INN", P1MapReports.Report(Window(), run.WaitLines)[0]);
    }
}
