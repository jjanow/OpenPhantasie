using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s224: the C64 end screens' steps left by s223 (<c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.4, II's 8.1):
/// I's closing page after the grant (until a key), the party wipe's UNDEAD name row and the ghost → figure morph during
/// the copy, and II's <c>WIN</c> as the C64 runs it (<see cref="C64WinScreen"/>).
/// </summary>
public class C64EndScreenStepsTests
{
    private static C64EndingLayout Ending(bool closing) => new()
    {
        VblHz = 50, JiffyHz = 60,
        Astral = new C64TextLine("a", 3, 23, 7, 2),
        Cloud = "ending.ghost", CloudLeftX = 66, CloudRightX = 216, CloudGap = 30, CloudFromY = 190, CloudToY = 100,
        CloudStepSeconds = 0.05, AfterRiseSeconds = 2, WipeX = 104, WipeY = 16, WipeWidth = 96, WipeHeight = 104,
        WipeFrames = 7, AfterWipeSeconds = 2, StrikeHand = "ending.arm", ThunderSeconds = 0.5, FigureSeconds = 0.3,
        AfterStrikeSeconds = 0.7, Smiles = new C64TextLine("s", 8, 24, 7, 3), Face = ["f"], FaceBlank = [0, 0, 1, 1],
        SpeechCol = 1, SpeechRow = 18, SpeechColour = 7, SpaceJiffies = 16, RandomJiffies = 8, Hand = ["h"],
        HandBlank = [0, 0, 1, 1], GrantSeconds = 12,
        ClosingLines = closing ? ["c1", "c2", "c3"] : [], ClosingCol = 0, ClosingRow = 2, ClosingColour = 5,
        ClosingKey = closing ? new C64TextLine("k", 7, 24, 5, 0) : null,
    };

    private static C64EndingScreen EndingScreen(bool closing) =>
        new(Ending(closing), new VictoryScene([new VictorySlot(Race.Human, true)], ["AB"]), ["AB"], "A", "S", () => 0.5,
            closing ? ["LINE 1", "LINE 2", "LINE 3"] : null, closing ? "(KEY)" : null);

    [Fact]
    public void I_ClosingPage_FollowsTheGrant_UntilAKey()
    {
        var screen = EndingScreen(closing: true);
        Assert.Equal(screen.GrantAt + 12, screen.ClosingAt, 6);
        screen.Key(screen.GrantAt + 1); // the C64 reads no key before its page
        Assert.False(screen.At(screen.ClosingAt - 0.01).Page);
        var page = screen.At(screen.ClosingAt + 0.01);
        Assert.True(page.Page);
        Assert.False(page.Done);
        Assert.Equal((-1, -1, -1, 0), (page.CloudY, page.Face, page.Hand, page.ZeusRows)); // Zeus and the sprites gone
        Assert.Equal(
            [new(0, 2, "LINE 1", 5), new(0, 3, "LINE 2", 5), new(0, 4, "LINE 3", 5), new(7, 24, "(KEY)", 5)],
            page.Text);
        Assert.True(screen.WaitsForKey(screen.ClosingAt + 100));
        Assert.False(screen.At(screen.ClosingAt + 100).Done);
        screen.Key(screen.ClosingAt + 5);
        Assert.Equal(screen.ClosingAt + 5, screen.DoneAt, 6);
        Assert.True(screen.At(screen.ClosingAt + 5).Done);
        Assert.False(screen.WaitsForKey(screen.ClosingAt + 6));
    }

    [Fact]
    public void I_WithoutClosingLines_EndsAfterTheGrant()
    {
        var screen = EndingScreen(closing: false);
        Assert.Equal(screen.ClosingAt, screen.DoneAt, 6);
        Assert.True(screen.At(screen.DoneAt).Done);
        Assert.False(screen.At(screen.DoneAt).Page);
        Assert.False(screen.WaitsForKey(screen.DoneAt + 1));
    }

    private static C64DefeatLayout Defeat(int? nameRow = 21) => new()
    {
        Temple = "t", Ghost = "g", Door = "d", Face = "f", Defeated = new C64TextLine("x", 6, 23, 2, 2), TextCol = 1, TextRow = 23,
        RowX2 = 172, RowGap = 35, RowY = 120, GhostSeconds = 0.3, AfterGhostsSeconds = 2, DoorX = 124, DoorY = 27,
        DoorSteps = 78, DoorStepSeconds = 0.1, FaceX = 124, FaceY = 31, DemonSeconds = 4, FateSoundSeconds = 1.17,
        FateCopySeconds = 4.1, FateSeconds = 2, EndSeconds = 6, UndeadNameRow = nameRow, UndeadNameCol = 1,
    };

    private static PartyDefeatResult Fates(params DefeatFate[] fates) => new()
    {
        Fates = [.. fates.Select((f, i) => (new Character { Name = $"M{i}" }, f))],
    };

    [Fact]
    public void Defeat_TheGhostMorphs_ByteByByte_DuringTheCopy_ThenTakesItsColour()
    {
        var screen = new C64DefeatScreen(Defeat(), Fates(DefeatFate.Resurrected, DefeatFate.Destroyed),
            ["A", "D", "F0", "F1"], "X");
        var t = screen.FatesAt;
        Assert.Equal(-1, screen.At(t + 1.0).MorphSlot); // the sound first
        var start = screen.At(t + 1.17 + 0.001);
        Assert.Equal((0, 0, 0), (start.MorphSlot, start.MorphBytes, start.Fates));
        Assert.Contains(start.Text, r => r.Text == "F0"); // the line before the copy (CEMETARY 0x8ee)
        var half = screen.At(t + 1.17 + 2.05 + 0.001);
        Assert.Equal((0, 32), (half.MorphSlot, half.MorphBytes)); // 64 passes over 4.1 s
        var done = screen.At(t + 1.17 + 4.1 + 0.001);
        Assert.Equal((-1, 1), (done.MorphSlot, done.Fates)); // the copy over: the fate's colour
        // A destroyed member's ghost morphs too, before its sprite goes off.
        var second = screen.At(t + 7.27 + 1.17 + 1.0);
        Assert.Equal(1, second.MorphSlot);
        Assert.InRange(second.MorphBytes, 15, 16);
    }

    [Fact]
    public void Defeat_AnUndeadFate_PrintsItsName_OnRow21_BeforeTheSound()
    {
        var screen = new C64DefeatScreen(Defeat(), Fates(DefeatFate.Undead, DefeatFate.Resurrected),
            ["A", "D", "M0 UNDEAD", "M1 OK"], "X");
        var during = screen.At(screen.FatesAt + 0.5);
        Assert.Contains(during.Text, r => r is { Col: 1, Row: 21, Text: "M0", Colour: 3 });
        Assert.DoesNotContain(during.Text, r => r.Row == 23); // its line after the sound
        Assert.Contains(screen.At(screen.FatesAt + 1.2).Text, r => r is { Row: 23, Text: "M0 UNDEAD", Colour: 3 });
        var next = screen.At(screen.FatesAt + 7.27 + 1.2);
        Assert.DoesNotContain(next.Text, r => r.Row == 21); // the rows cleared for the next fate; no name for a resurrection
        var none = new C64DefeatScreen(Defeat(nameRow: null), Fates(DefeatFate.Undead), ["A", "D", "M0"], "X");
        Assert.DoesNotContain(none.At(none.FatesAt + 0.5).Text, r => r.Row == 21);
    }

    private static C64WinLayout Win() => new()
    {
        IntroSeconds = 16.85, HallSeconds = 9.49, Crowd = ["c0", "c1"], CrowdY = 110, RowLeft = 86, RowTop = 75, SlotSize = 24,
        PresentColour = 10, TextColour = 0, HeaderAt = [(2, 20), (0, 21)], ForSeconds = 4.17, TitleSeconds = 1.09,
        TitleRow = 20, TitleCentre = 20, TitleWord = "w", TitleFrom = "f",
        CheerLine = new C64TextLine("cheer", 10, 21, 0, 0), CheerFrameAt = 0.16, CheerFrameSeconds = 9.93,
        CheerLineSeconds = 10.15, CheerSeconds = 10.23, WandsAt = [(2, 20), (0, 21), (0, 22), (0, 23)], WandsKey = "k",
    };

    private static VictoryScene WinScene() =>
        new([new(Race.Human, true), new(Race.Elf, true), new(Race.Dwarf, false)], ["SPEAKS", "W1", "W2", "W3", " "])
        {
            IntroPage = ["i1"], HeaderLines = ["SPEAKS", " 'PRESENTING'"], Titles = ["t0", "t1"], ClosingPage = ["c1"],
            TitleParts = [new("LOUIS", "MIGHTY", "THE NETHERWORLD"), new("THUNDAR", "BOLD", "GELNOR")],
        };

    private static C64WinScreen WinScreen() =>
        new(Win(), WinScene(), "THE {0} ONE", "FROM {0}", "THE CROWD GOES WILD!", " (PRESS RET)");

    [Fact]
    public void II_Intro_ThenTheHallAlone_ThenTheCrowdAndRow_WithTheHeader()
    {
        var screen = WinScreen();
        Assert.Equal(C64WinScreen.PageKind.Intro, screen.At(1).Page);
        screen.Key(2); // the intro reads no key
        Assert.Equal(C64WinScreen.PageKind.Intro, screen.At(16.8).Page);
        Assert.Equal(16.85, screen.HallAt, 6);
        var hall = screen.At(17);
        Assert.Equal((C64WinScreen.PageKind.None, false), (hall.Page, hall.Band));
        Assert.Empty(hall.Text);
        Assert.Equal(16.85 + 9.49, screen.BandAt, 6);
        var header = screen.At(screen.BandAt + 0.01);
        Assert.Equal((true, 0, -1), (header.Band, header.Crowd, header.Presented)); // the crowd's first frame from the start
        Assert.Equal([new(2, 20, "SPEAKS", 0), new(0, 21, " 'PRESENTING'", 0)], header.Text);
    }

    [Fact]
    public void II_Cheer_ClearsThePanel_ShowsTheSecondFrame_ForTheSoundOnly()
    {
        var screen = WinScreen();
        var cheer = screen.BandAt + 4.17;
        var lead = screen.At(cheer + 0.1);
        Assert.Equal([new(10, 21, "THE CROWD GOES WILD!", 0)], lead.Text);
        Assert.Equal(0, lead.Crowd);
        Assert.Equal(1, screen.At(cheer + 0.2).Crowd);
        Assert.Equal(1, screen.At(cheer + 10.0).Crowd);
        Assert.Equal(0, screen.At(cheer + 10.1).Crowd); // pointers - 8
        Assert.Empty(screen.At(cheer + 10.2).Text); // the panel cleared
    }

    [Fact]
    public void II_EachTitledMember_IsPresented_OnThreeCentredRows()
    {
        var screen = WinScreen();
        var first = screen.BandAt + 4.17 + 10.23; // after the header's cheer
        var wait = screen.At(first + 4.0);
        Assert.Equal((-1, 0), (wait.Presented, wait.Text.Count)); // FOR 4000 before the member
        var shown = screen.At(first + 4.2);
        Assert.Equal(0, shown.Presented);
        // WIN 0x9fb: 20 - LEN / 2 (INT); "THE "V$" ONE" at 16 - LEN(V$) / 2.
        Assert.Equal([new(17, 20, "LOUIS", 0), new(13, 21, "THE MIGHTY ONE", 0), new(10, 22, "FROM THE NETHERWORLD", 0)],
            shown.Text);
        Assert.Equal(0, screen.At(first + 4.17 + 1.09 + 4.1).Presented); // titles kept through FOR 1000 and FOR 4000
        var memberCheer = screen.At(first + 4.17 + 1.09 + 4.17 + 1);
        Assert.Equal((0, 1), (memberCheer.Presented, memberCheer.Crowd)); // still light red while the crowd cheers
        var second = first + 4.17 + 1.09 + 4.17 + 10.23;
        Assert.Equal(-1, screen.At(second + 1).Presented); // back in its idle pose
        Assert.Equal(1, screen.At(second + 4.2).Presented);
        Assert.Equal(2, screen.Presentations); // Core's titles: the third (dead) member is not presented
    }

    [Fact]
    public void II_WandsPrompt_ThenTheClosingPage_EachUntilAKey()
    {
        var screen = WinScreen();
        var wands = screen.WandsAt;
        Assert.Equal(screen.BandAt + 4.17 + 10.23 + 2 * (4.17 + 1.09 + 4.17 + 10.23), wands, 6);
        var prompt = screen.At(wands + 1);
        Assert.True(prompt.WaitsForKey);
        Assert.Equal([new(2, 20, "SPEAKS", 0), new(0, 21, "W1", 0), new(0, 22, "W2", 0), new(0, 23, "W3 (PRESS RET)", 0)],
            prompt.Text);
        Assert.Equal(0, prompt.Crowd);
        Assert.True(prompt.Band);
        Assert.False(screen.At(wands + 1000).Done);
        screen.Key(wands + 2);
        var page = screen.At(wands + 2.01);
        Assert.Equal((C64WinScreen.PageKind.Closing, false, true), (page.Page, page.Band, page.WaitsForKey));
        screen.Key(wands + 5);
        Assert.True(screen.At(wands + 5).Done);
        Assert.Equal(wands + 5, screen.DoneAt, 6);
    }

    [OriginalDataFact]
    public void Packs_CarryTheClosingPage_TheMorphRow_AndWinsSteps()
    {
        var p1 = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("c64")!;
        var c64 = p1.EndingScene!.C64!;
        Assert.Equal([.. Enumerable.Range(1, 9).Select(i => $"c64.endgame.closing_{i}")], c64.ClosingLines);
        // s226 (D-090): the Olympus outcome rewords line 2 only, at the C64's 39 columns, and keeps "the-" for line 3's "re,".
        var scene = new VictoryScene([new VictorySlot(Race.Human, true)], ["AB"]);
        Assert.Same(c64.ClosingLines, Game1.C64ClosingLines(c64, scene));
        var olympus = Game1.C64ClosingLines(c64, scene with { ToOlympus = true });
        Assert.Equal([.. c64.ClosingLines.Select(id => id == "c64.endgame.closing_2" ? "c64.endgame.closing_2.olympus" : id)],
            olympus);
        var table = StringTable.ForGame(GameId.P1);
        Assert.Equal(table.Get("c64.endgame.closing_2").Length, table.Get("c64.endgame.closing_2.olympus").Length);
        Assert.Contains("Olympic Plane", table.Get("c64.endgame.closing_2.olympus"));
        Assert.EndsWith("the-", table.Get("c64.endgame.closing_2.olympus"));
        Assert.Equal((0, 2, 5), (c64.ClosingCol, c64.ClosingRow, c64.ClosingColour));
        Assert.Equal(("c64.endgame.closing_key", 7, 24), (c64.ClosingKey!.Text, c64.ClosingKey.Col, c64.ClosingKey.Row));
        Assert.Equal((21, 1), (p1.C64Defeat!.UndeadNameRow, p1.C64Defeat.UndeadNameCol));
        var p2 = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("c64")!;
        var win = p2.EndingScene!.C64Win!;
        Assert.All(win.Crowd, n => Assert.NotNull(p2.Resolve(n)));
        Assert.EndsWith("ending/crowd-0.png", p2.Resolve(win.Crowd[0]));
        Assert.Equal([(2, 20), (0, 21)], win.HeaderAt);
        Assert.Equal(("c64.p2.crowd_goes_wild", 10, 21), (win.CheerLine.Text, win.CheerLine.Col, win.CheerLine.Row));
        Assert.Equal(10.23, win.CheerSeconds, 6);
    }
}
