using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using Xunit;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s153 (Amiga <c>FORMAT.md</c> "Overworld / dungeon map graphics"): I Amiga's party sprite under the map's questions, and
/// its water cycle that steps only from the input wait loop.
/// </summary>
public class P1MapSpriteAndCycleTests
{
    private static GraphicsPack I => GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")!;
    private static GraphicsPack St => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    [OriginalDataFact]
    public void IPack_HidesTheOverworldPartyUnderAQuestion_OnlyBehindAmigaScreens()
    {
        // h86 +0 / +0x132: MoveSprite(party, x, 200) before Request, back after EndRequest.
        Assert.True(I.PartyHiddenUnderPrompts);
        Assert.True(MapPartySprite.HiddenUnderPrompt(I, originalScreens: true, promptOpen: true));
        Assert.False(MapPartySprite.HiddenUnderPrompt(I, originalScreens: true, promptOpen: false));
        Assert.False(MapPartySprite.HiddenUnderPrompt(I, originalScreens: false, promptOpen: true));
        Assert.False(MapPartySprite.HiddenUnderPrompt(null, originalScreens: true, promptOpen: true));
    }

    [OriginalDataFact]
    public void StPack_KeepsItsPartyAndItsVblCycle()
    {
        Assert.False(St.PartyHiddenUnderPrompts);
        Assert.False(MapPartySprite.HiddenUnderPrompt(St, originalScreens: true, promptOpen: true));
        Assert.False(St.ColourCycle!.InputWaitOnly); // the ST's VBL routine 0x1382e runs on its own
    }

    [OriginalDataFact]
    public void IPack_WaterCycleRunsOnlyWhileTheMapWaits_WithTheEightSecondClock()
    {
        var cycle = I.ColourCycle!;
        Assert.True(cycle.InputWaitOnly);       // h77 +0x178's only caller is h80 +0x1ac
        Assert.Equal(8, cycle.ClockWrapSeconds); // h80 +0xd2a: andi.l #7 on the seconds
        Assert.Equal(0.2, cycle.StepSeconds, 9);
        for (var step = 0; step < 7; step++)
            Assert.Equal(step % 3, cycle.Phase(cycle.SecondsAtStep(step)));
    }

    private static ColourCycle Cycle(double wrap = 0) =>
        new(["#000001", "#000002", "#000003"], 12, 60, ["overworld.region."]) { InputWaitOnly = true, ClockWrapSeconds = wrap };

    [Fact]
    public void Clock_StepsEveryStepTimeWhileWaiting()
    {
        var clock = new InputWaitCycleClock();
        var cycle = Cycle();
        clock.Poll(1.00, true, cycle); // the first idle poll steps
        Assert.Equal(1, clock.Steps);
        clock.Poll(1.15, true, cycle);
        Assert.Equal(1, clock.Steps);
        clock.Poll(1.20, true, cycle);
        Assert.Equal(2, clock.Steps);
        clock.Poll(1.39, true, cycle);
        Assert.Equal(2, clock.Steps);
        clock.Poll(1.41, true, cycle);
        Assert.Equal(3, clock.Steps);
    }

    [Fact]
    public void Clock_StandsStillWhileBusy_ThenStepsOnceAtOnce_NoCatchingUp()
    {
        var clock = new InputWaitCycleClock();
        var cycle = Cycle();
        clock.Poll(1.0, true, cycle);
        for (var t = 1.05; t < 4.0; t += 0.05)
            clock.Poll(t, false, cycle); // a box, a requester: the loop does not poll
        Assert.Equal(1, clock.Steps);
        clock.Poll(4.0, true, cycle); // back in the wait loop: one step, not fifteen
        Assert.Equal(2, clock.Steps);
        clock.Poll(4.1, true, cycle);
        Assert.Equal(2, clock.Steps);
        clock.Poll(4.2, true, cycle);
        Assert.Equal(3, clock.Steps);
    }

    [Fact]
    public void Clock_StepsEarlyWhenTheSecondsPassAMultipleOfEight()
    {
        var clock = new InputWaitCycleClock();
        var cycle = Cycle(wrap: 8);
        clock.Poll(7.95, true, cycle);
        clock.Poll(8.01, true, cycle); // 0.06 s later, but (8 mod 8) < (7 mod 8): negative, compared unsigned
        Assert.Equal(2, clock.Steps);
        clock.Poll(8.10, true, cycle);
        Assert.Equal(2, clock.Steps);
    }

    [Fact]
    public void PackLoader_RejectsAnUnknownRunsWhile()
    {
        var top = Path.Combine(Path.GetTempPath(), "phantasie-p1cycle-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(top, "testgame");
        Directory.CreateDirectory(Path.Combine(dir, "graphics"));
        Directory.CreateDirectory(Path.Combine(dir, "art"));
        File.WriteAllText(Path.Combine(dir, "graphics", "index.json"),
            """{ "game": "testgame", "default_order": ["p"], "packs": ["p.json"] }""");
        File.WriteAllText(Path.Combine(dir, "graphics", "p.json"), """
            { "id": "p", "game": "testgame", "platform": "amiga", "root": "art",
              "palette": ["#000000", "#111111", "#222222"],
              "colour_cycle": { "registers": [0, 1, 2], "period_vbl": 12, "vbl_hz": 60, "runs_while": "sometimes" } }
            """);
        try
        {
            Assert.Throws<InvalidDataException>(() => GraphicsPackCatalog.Load(dir, searchFrom: dir));
        }
        finally
        {
            Directory.Delete(top, recursive: true);
        }
    }
}
