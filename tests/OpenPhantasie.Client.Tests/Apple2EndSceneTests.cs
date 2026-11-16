using System.Text.Json.Nodes;
using OpenPhantasie.Client.Audio;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s224: Phantasie I Apple II's end scenes (<c>apple2_scenes</c> of the shipped <c>apple2.json</c>, GRAPHICS.md 8) as
/// <see cref="Apple2Scene"/> runs them: the statements' times, the GETs (one per member on the wipe, two on the ending) with
/// the keyboard strobe's latch, the party row's places (two figures for a lone member on CEMETARY, the member's own body on
/// FLASH?), Core's fates printed as the Apple prints its own, the text page's scrolling, the thunder's clicks (the ROM run's
/// counts) and the speaker renderer. A one-dot shape table stands in for <c>SHAPES</c> (no original data needed).
/// </summary>
public class Apple2EndSceneTests
{
    private static readonly Lazy<Apple2ScenesLayout> Shipped = new(() =>
    {
        var path = Path.Combine(GameDataRepository.DefaultPhantasie1Path, "graphics", "apple2.json");
        var scenes = JsonNode.Parse(File.ReadAllText(path))!["apple2_scenes"]!.ToJsonString();
        return Apple2ScenesLayout.Parse(scenes, "/nowhere");
    });

    /// <summary>58 shapes, each one plotted dot (vector byte 4: plot, then move up).</summary>
    private static byte[] Dots()
    {
        const int n = 58;
        var table = new byte[2 + 2 * n + 2 * n];
        table[0] = n;
        for (var i = 0; i < n; i++)
        {
            var at = 2 + 2 * n + 2 * i;
            table[2 + 2 * i] = (byte)at;
            table[3 + 2 * i] = (byte)(at >> 8);
            table[at] = 4;
        }
        return table;
    }

    private static Apple2SceneMember[] Members(int n) => [.. Enumerable.Range(0, n).Select(i => new Apple2SceneMember($"M{i}", 19))];

    private static int Keys(Apple2Scene scene, double step = 0.05, int limit = 100)
    {
        var keys = 0;
        var key = false;
        for (var i = 0; i < 100000 && !scene.Done; i++)
        {
            if (scene.WaitingForKey)
            {
                keys++;
                key = true;
                if (keys > limit)
                    break;
            }
            scene.Advance(step, ref key);
        }
        return keys;
    }

    [Fact]
    public void ShippedPack_HasBothScenes_TheThunder_AndTheBodies()
    {
        var layout = Shipped.Value;
        Assert.NotEmpty(layout.Defeat);
        Assert.NotEmpty(layout.Ending);
        Assert.Equal(15, layout.Sounds["thunder"].Calls.Count);
        Assert.EndsWith(Path.Combine("scenes", "shapes.bin"), layout.ShapesFile);
        // TOWN 2350-2356: HUMAN / ELF 19, robed 27 for MONK / PRIEST / WIZARD; small races 25; races 7+ 26.
        Assert.Equal(19, layout.Bodies.For(0, 3));
        Assert.Equal(27, layout.Bodies.For(2, 5));
        Assert.Equal(25, layout.Bodies.For(1, 1));
        Assert.Equal(26, layout.Bodies.For(9, 4));
        Assert.Equal(26, layout.Bodies.For(15, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void Defeat_WaitsForAKeyPerMember(int members)
    {
        var scene = new Apple2Scene(Shipped.Value.Defeat, Dots(), Members(members), id => id,
            [.. Enumerable.Range(0, members).Select(i => (Apple2SceneFate)(i % 3))]);
        Assert.Equal(members, Keys(scene));
        Assert.True(scene.Done);
    }

    [Fact]
    public void Defeat_RunsAsTheRomTimedIt_TheDanceAbout21Seconds_TheAstralAndDemonWaits()
    {
        var layout = Shipped.Value;
        var dance = layout.Defeat.Single(s => s.Op == "repeat");
        Assert.Equal(70, dance.Count);
        Assert.InRange(dance.Count * dance.Steps.Sum(s => s.Seconds), 20.5, 22.0);
        // 670: FOR 9000 after the astral line; 700: FOR 7000 after the demon's.
        Assert.InRange(layout.Defeat.Single(s => s.Text == "apple2.defeat.astral").Seconds, 8.8, 9.1);
        Assert.InRange(layout.Defeat.Single(s => s.Text == "apple2.defeat.demon").Seconds, 6.9, 7.1);
        var scene = new Apple2Scene(layout.Defeat, Dots(), Members(4), id => id, [.. Enumerable.Repeat(Apple2SceneFate.Resurrected, 4)]);
        Assert.InRange(scene.Duration, 40, 44);
    }

    [Fact]
    public void Defeat_PartyRow_ALoneMemberStandsTwice()
    {
        var row = Shipped.Value.Defeat.Single(s => s.Op == "party");
        Assert.Equal([65, 185], row.Xs[0]);
        Assert.Equal([65, 185], row.Xs[1]);
        Assert.Equal([65, 89, 113, 137, 161, 185], row.Xs[5]);
        Assert.Equal([19, 20], row.Shapes);
        var one = new Apple2Scene([row], Dots(), Members(1), id => id);
        var two = new Apple2Scene([row], Dots(), Members(2), id => id);
        Assert.Equal(one.Statements, two.Statements);
    }

    [Fact]
    public void Ending_FiguresAreTheMembersOwnBodies_AtFLASHsPlaces()
    {
        var row = Shipped.Value.Ending.Single(s => s.Op == "party");
        Assert.Equal([120], row.Xs[0]);
        Assert.Equal([35, 95, 155, 215], row.Xs[3]);
        Assert.Equal([20, 0], row.Shapes); // arm 20, then the body (record byte 168)
        var scene = new Apple2Scene([row], Dots(), [new("A", 25), new("B", 27)], id => id);
        scene.RunStatements(scene.Statements);
        // SCALE 2 dots at (35, 159) and (215, 159): both figures drawn.
        Assert.NotEqual(0, scene.Page.Mem[Apple2Hires.Offset(159, 35 / 7)]);
        Assert.NotEqual(0, scene.Page.Mem[Apple2Hires.Offset(159, 215 / 7)]);
    }

    [Fact]
    public void Ending_TwoKeys_EightThunders()
    {
        var scene = new Apple2Scene(Shipped.Value.Ending, Dots(), Members(3), id => id);
        var sounds = 0;
        var keys = 0;
        var key = false;
        for (var i = 0; i < 100000 && !scene.Done; i++)
        {
            if (scene.WaitingForKey)
            {
                keys++;
                key = true;
            }
            scene.Advance(0.02, ref key);
            sounds += scene.TakeSounds().Count(s => s == "thunder");
        }
        Assert.Equal(2, keys);
        Assert.Equal(8, sounds);
    }

    [Fact]
    public void AKeyPressedEarly_IsLatchedForTheNextGet()
    {
        var scene = new Apple2Scene(Shipped.Value.Ending, Dots(), Members(1), id => id);
        var key = true; // pressed at once, long before line 100's GET
        scene.Advance(1, ref key);
        Assert.True(key);
        for (var i = 0; i < 400 && !scene.WaitingForKey && key; i++)
            scene.Advance(0.05, ref key);
        Assert.False(key); // taken by the GET at 100: no wait there
        Assert.False(scene.WaitingForKey);
    }

    [Fact]
    public void Fates_PrintCoresFatesInTheApplesWords()
    {
        // Stand-ins for the Apple's words (an import writes them): LEAD for "you have been", the fates' words.
        var text = new Dictionary<string, string>
        {
            ["apple2.defeat.you_have_been"] = "LEAD", ["apple2.defeat.destroyed"] = "GONE",
            ["apple2.defeat.resurrected"] = "BACK", ["apple2.defeat.undead"] = "UNDEAD WORDS",
        };
        var fates = Shipped.Value.Defeat.Single(s => s.Op == "fates");
        var scene = new Apple2Scene([new Apple2Step { Op = "vtab", Row = 24 }, fates], Dots(), [new("AARON", 19), new("VIS", 25)],
            id => text.GetValueOrDefault(id, id), [Apple2SceneFate.Undead, Apple2SceneFate.Destroyed]);
        var key = false;
        while (!scene.WaitingForKey)
            scene.Advance(0.01, ref key);
        // CEMETARY 720 / 1100: the name and the lead, a return, the undead words, a return.
        Assert.Equal("AARON LEAD", scene.Text.RowText(21).TrimEnd());
        Assert.Equal("UNDEAD WORDS", scene.Text.RowText(22).TrimEnd());
        key = true;
        scene.Advance(0.01, ref key);
        while (!scene.WaitingForKey)
            scene.Advance(0.01, ref key);
        Assert.Equal("VIS LEAD GONE", scene.Text.RowText(22).TrimEnd());
    }

    [Fact]
    public void TextPage_ScrollsFromTheBottomRow()
    {
        var page = new Apple2TextPage();
        page.Vtab(24);
        page.Print("ONE");
        page.Return();
        page.Print("TWO");
        page.Return();
        Assert.Equal(("ONE", "TWO", ""), (page.RowText(21).Trim(), page.RowText(22).Trim(), page.RowText(23).Trim()));
        page.Home();
        Assert.Equal((0, 0), (page.Column, page.Line));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(250, 4, 4)]
    [InlineData(255, 7, 7)]
    [InlineData(1, 4, 614)]
    [InlineData(1, 9, 1894)]
    [InlineData(160, 2, 2)]
    public void Speaker_ClickCounts_AreTheRomRuns(int pitch, int duration, int clicks)
    {
        // FLASH? 1100's CALL 770 enters INFO with X = 157, Y = 2 (the 6502 emulator's run).
        Assert.Equal(clicks, Apple2Speaker.Clicks(pitch, duration, 157, 2).Count);
    }

    [Fact]
    public void Speaker_PitchSetsThePeriod_TenCyclesAStep()
    {
        var c = Apple2Speaker.Clicks(250, 4, 157, 2);
        // A period: LDA $C030, 250 DEY / DEX passes (10 cycles, one less on the last), LDX, JMP, plus a Y wrap's 7.
        Assert.Equal(10 * 250 + 10 + 7, c[2] - c[1]);
        var thunder = Shipped.Value.Sounds["thunder"];
        Assert.Equal(7810, thunder.Calls.Sum(k => Apple2Speaker.Clicks(k.Pitch, k.Duration, thunder.EntryX, thunder.EntryY).Count));
    }

    [Fact]
    public void SpeakerRenderer_RendersTheThunder_AudibleAndBounded()
    {
        var r = new Apple2SpeakerRenderer(Shipped.Value.Sounds["thunder"], Shipped.Value.ClockHz);
        var pcm = r.RenderAll();
        Assert.InRange(pcm.Length / 2.0 / r.SampleRate, 0.5, 0.6);
        Assert.Contains(pcm, v => Math.Abs(v) > 0.05f);
        Assert.All(pcm, v => Assert.InRange(v, -1f, 1f));
    }
}
