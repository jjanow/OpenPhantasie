using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s154, I Amiga's waits where its code blocks (FORMAT.md "Sound effects", "The combat screen", "The party defeat"): the
/// step tone before the move, a question's sound before the question, the monster pictures' read before the fight; the
/// combat resurrection's cleared line; the defeat door's raw tone, the 5th defeat's sign and the z6font lines; the water's
/// stored colours after a redraw.
/// </summary>
public partial class P1MapHoldsTests
{
    private static GraphicsPack Pack => GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!;

    private static double Hold(int sound)
    {
        var fx = Pack.DosoundEffects!;
        return fx.TryLoadImage() is { } image ? fx.Hold(image, sound).Seconds : 0;
    }

    [OriginalDataFact]
    public void StepTone_ThenTheMove_AfterTheToneHold()
    {
        var holds = new P1MapHolds();
        var tone = Hold(31); // ~0.075 s (s153 model)
        var order = new List<string> { "tone" };
        holds.Defer(10, tone, () => order.Add("move"));
        Assert.True(holds.Busy(10));
        holds.Update(10 + tone / 2);
        Assert.Equal(["tone"], order);                     // the move waits for the tone
        holds.Update(10 + tone);
        Assert.Equal(["tone", "move"], order);
        Assert.False(holds.Busy(10 + tone));
    }

    [OriginalDataFact]
    public void StepTone_ThenMove_Then19_ThenTheQuestion()
    {
        var holds = new P1MapHolds();
        var prompt = new object();
        var (tone, s19) = (Hold(31), Hold(19));             // 0.075 s, then 0.43 s (s153 model)
        holds.Defer(0, tone, () => holds.HoldPrompt(tone, s19, prompt)); // the move opens "A TOWN. ENTER?"
        holds.Update(tone);
        Assert.True(holds.PromptHeld(prompt, tone + 0.01)); // the question hidden while 19 holds
        Assert.True(holds.Busy(tone + s19 - 0.01));
        Assert.False(holds.PromptHeld(prompt, tone + s19)); // then shown
        Assert.False(holds.Busy(tone + s19));
    }

    [OriginalDataFact]
    public void BlackKnights27_HoldsItsMenu()
    {
        var holds = new P1MapHolds();
        var menu = new object();
        var s27 = Hold(27);                                  // h89 +0x158: six copies, ~1.34 s
        Assert.InRange(s27, 1.2, 1.5);
        holds.HoldPrompt(5, s27, menu);
        Assert.True(holds.PromptHeld(menu, 5 + s27 - 0.01));
        Assert.False(holds.PromptHeld(new object(), 5.1));   // another question is not held
        Assert.False(holds.PromptHeld(menu, 5 + s27));
    }

    [Fact]
    public void SoundOff_NoWaits()
    {
        var holds = new P1MapHolds();
        var moved = false;
        holds.Defer(0, 0, () => moved = true);               // user s129: no sound waits with the Sound switch off
        Assert.True(moved);
        Assert.False(holds.Busy(0));
        holds.HoldPrompt(0, 0, new object());
        Assert.False(holds.Busy(0));
    }

    [Fact]
    public void ADeferralWhileBusy_RunsAfterTheFirst()
    {
        var holds = new P1MapHolds();
        var order = new List<int>();
        holds.Defer(0, 0.1, () => order.Add(1));
        holds.Defer(0.05, 0.1, () => order.Add(2));
        holds.Update(0.1);
        Assert.Equal([1], order);
        Assert.True(holds.Busy(0.15));
        holds.Update(0.2);
        Assert.Equal([1, 2], order);
        holds.Reset();
        Assert.False(holds.Busy(0.2));
    }

    /// <summary>
    /// s162: the town TRANSPORTATION on I Amiga (h26 <c>+0xdb6</c> / <c>+0xe4a</c> / <c>+0xe30</c>): the list's tone, then
    /// 22, then the fade, each after the one before has ended (Game1's PlayP1CueInTurn and the fade's deferred start, all
    /// queued in the same frame).
    /// </summary>
    [Fact]
    public void TransportToneThen22ThenTheFade_InTurn()
    {
        var holds = new P1MapHolds();
        var order = new List<(string What, double At)>();
        var now = 0.0;
        void Play(string what, double hold) => holds.Defer(now, 0, () =>
        {
            order.Add((what, now));
            holds.Defer(now, hold, static () => { });
        });
        Play("tone", 0.075);
        Play("22", 0.61);
        holds.Defer(now, 0, () => order.Add(("fade", now)));
        for (; now < 1; now += 0.001)
            holds.Update(now);
        Assert.Equal(["tone", "22", "fade"], order.Select(o => o.What));
        Assert.Equal(0, order[0].At);
        Assert.InRange(order[1].At, 0.075, 0.077);
        Assert.InRange(order[2].At, 0.685, 0.687);
        Assert.False(holds.Busy(now));
    }

    [OriginalDataFact]
    public void ShapeCache_ReadsOnlyMissingMonsters_ThreeSlots()
    {
        var cache = new P1MonsterShapeCache(3);
        Assert.Equal(1, cache.Load([7]));                    // empty at boot: one read
        Assert.Equal(0, cache.Load([7]));                    // cached: no read, no wait
        Assert.Equal(2, cache.Load([7, 9, 9, 11]));          // a group with an earlier group's monster shares its slot
        Assert.Equal(0, cache.Load([11, 9, 7]));
        Assert.Equal(1, cache.Load([12, 7, 9]));             // the first slot this fight does not use (11's) takes 12
        Assert.Equal(1, cache.Load([11]));
        var l = Pack.CombatField!;
        Assert.Equal(3, l.ShapeCacheSlots);
        Assert.InRange(l.ShapeLoadSeconds, 2.77, 3.77);      // s153 live 3.62 / 3.77 / 2.77 [~]
    }

    [OriginalDataFact]
    public void Resurrection_12BeforeTheLine_ThenTheLineCleared()
    {
        var l = Pack.CombatField!;
        var a = new Character { Name = "A", Race = Race.Human };
        var line = GameText.F("core.magic.x_is_resurrected", "A");
        var run = P1CombatFieldRun.Plan(l, [line], [], [a], [], _ => false, 2, Hold, roundEnd: false);
        var shown = run.Lines.Single(x => x.Text == line);
        Assert.Contains(run.Lines, x => x.Text == " " && x.PrintMs >= shown.EndMs); // +0xb80(" ", -1) after its wait
        Assert.Equal(line, run.At(shown.PrintMs + 1).Text);
        Assert.Equal(" ", run.At(shown.EndMs + 1).Text);
    }

    [OriginalDataFact]
    public void Clock_ShowsStoredColoursAfterARedraw_UntilTheNextStep()
    {
        var cycle = Pack.ColourCycle!;
        var clock = new InputWaitCycleClock();
        clock.Poll(0, true, cycle);
        clock.Poll(cycle.StepSeconds, true, cycle);
        Assert.Equal(2, clock.Steps);
        clock.Redraw();
        Assert.True(clock.StoredShown);
        clock.Poll(cycle.StepSeconds + 0.5, false, cycle); // a box: no step, the stored colours stay
        Assert.True(clock.StoredShown);
        clock.Poll(cycle.StepSeconds + 0.6, true, cycle);  // the next step: the kept rotation + 1
        Assert.False(clock.StoredShown);
        Assert.Equal(3, clock.Steps);
    }

    [Fact]
    public void MovesPressedInAHold_AreMadeInOrder_OneAfterEachHold()
    {
        // s156: the port keeps every event of a one-step tone's hold (drained only before the sound); h65 takes one a call.
        var q = new P1MoveQueue();
        q.Press((1, 0));
        q.Press((0, 1));
        Assert.Equal((1, 0), q.Next(mapWaits: true));
        Assert.Equal((0, 1), q.Next(mapWaits: true)); // after the first move's own tone
        Assert.Null(q.Next(mapWaits: true));
    }

    [Fact]
    public void AQuestionThatOpens_DropsTheRest()
    {
        var q = new P1MoveQueue();
        q.Press((1, 0));
        q.Press((1, 0));
        Assert.Equal((1, 0), q.Next(mapWaits: true)); // onto a town: "A TOWN. ENTER?" opens
        Assert.Null(q.Next(mapWaits: false));
        Assert.Equal(0, q.Count);
    }
}
