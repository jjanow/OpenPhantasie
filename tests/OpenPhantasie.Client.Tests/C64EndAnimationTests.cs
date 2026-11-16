using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s223: the C64 I ending (<c>P.END</c>) and I / II party wipe (<c>CEMETARY</c>) over time
/// (<c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.4): the cloud row's places, the rise, the bottom-up wipe, a
/// strike per slot, the stacked word-by-word speech, the face and hand cycles; the ghosts one by one, the rising door, the
/// fates in their colours (a destroyed member's slot empty).
/// </summary>
public class C64EndAnimationTests
{
    private static C64EndingLayout Ending(double overhead = 0, double letter = 0) => new()
    {
        VblHz = 50, JiffyHz = 60,
        Astral = new C64TextLine("core.defeat.arrived_at_astral_plane", 3, 23, 7, 2),
        Cloud = "ending.ghost", CloudMc1 = 7, CloudLeftX = 66, CloudRightX = 216, CloudGap = 30, CloudFromY = 190,
        CloudToY = 100, CloudStepSeconds = 0.05, CloudStepOverhead = overhead, CloudFlash = [7, 2], CloudFlashSteps = 2,
        AfterRiseSeconds = 2, WipeX = 104, WipeY = 16, WipeWidth = 96, WipeHeight = 104, WipeFrames = 7, AfterWipeSeconds = 2,
        StrikeHand = "ending.arm", StrikeX = 178, StrikeY = 12, ThunderSeconds = 0.5, FigureSeconds = 0.3,
        AfterStrikeSeconds = 0.7, OkColour = 7, DeadColour = 15,
        Smiles = new C64TextLine("c64.endgame.zeus_smiles", 8, 24, 7, 3),
        Face = [.. Enumerable.Range(0, 8).Select(i => $"ending.face.{i}")], FaceX = 137, FaceY = 27, FaceBlank = [144, 32, 16, 16],
        AnimateVbl = 10, SpeechCol = 1, SpeechRow = 18, SpeechColour = 7, LetterSeconds = letter, SpaceJiffies = 16,
        RandomJiffies = 8, Hand = [.. Enumerable.Range(0, 6).Select(i => $"ending.hand.{i}")], HandX = 176, HandY = 24,
        HandBlank = [176, 24, 24, 16], GrantSeconds = 12,
    };

    private static VictoryScene Scene(params bool[] granted) =>
        new([.. granted.Select(g => new VictorySlot(Race.Human, g))], ["AB CD", "EF"]);

    private static C64EndingScreen Screen(C64EndingLayout layout, VictoryScene scene, double rnd = 0.5) =>
        new(layout, scene, scene.SpeechLines, "ASTRAL", "SMILES", () => rnd);

    [Theory]
    [InlineData(1, new[] { 66 })]
    [InlineData(2, new[] { 66, 216 })]
    [InlineData(3, new[] { 66, 36, 216 })]
    [InlineData(4, new[] { 66, 36, 216, 246 })]
    [InlineData(5, new[] { 66, 36, 6, 216, 246 })]
    [InlineData(6, new[] { 66, 36, 6, 216, 246, 276 })]
    public void Clouds_FlankZeus_ByPEND(int slots, int[] xs) =>
        // PL = INT(PO / 2 + 1.1): slots 0..PL-1 leftwards from VIC x 90, the rest rightwards from 240.
        Assert.Equal(xs, C64EndingScreen.CloudXs(slots, Ending()));

    [Fact]
    public void Astral_ThenTheCloudsRise_FlashingEveryTwoSteps()
    {
        var screen = Screen(Ending(), Scene(true, true));
        var f = screen.At(1);
        Assert.Equal(-1, f.CloudY);
        Assert.Contains(f.Text, r => r is { Row: 23, Col: 3, Text: "ASTRAL" });
        Assert.Equal(2, screen.RiseAt);
        Assert.Equal(91, screen.RiseSteps); // VIC y 240 down to 150
        var rise = screen.At(2.0001);
        Assert.Equal((190, 7), (rise.CloudY, rise.CloudColour));
        Assert.Empty(rise.Text); // the screen cleared
        Assert.Equal((189, 7), (screen.At(2 + 0.05 * 1.5).CloudY, screen.At(2 + 0.05 * 1.5).CloudColour));
        Assert.Equal((188, 2), (screen.At(2 + 0.05 * 2.5).CloudY, screen.At(2 + 0.05 * 2.5).CloudColour));
        var top = screen.At(screen.WipeAt - 0.01);
        Assert.Equal((100, 7), (top.CloudY, top.CloudColour)); // MC1 back to yellow
        Assert.Equal(0, top.ZeusRows);
        Assert.Equal(2 + 91 * 0.05 + 2, screen.WipeAt, 6);
    }

    [Fact]
    public void Zeus_IsWipedIn_ARowEverySevenFrames()
    {
        var screen = Screen(Ending(), Scene(true));
        Assert.Equal(0, screen.At(screen.WipeAt + 0.13).ZeusRows); // 7 frames at 50 Hz = 0.14 s
        Assert.Equal(1, screen.At(screen.WipeAt + 0.15).ZeusRows);
        Assert.Equal(52, screen.At(screen.WipeAt + 52 * 0.14 + 0.001).ZeusRows);
        Assert.Equal(104, screen.At(screen.StrikesAt - 0.5).ZeusRows);
        Assert.Equal(screen.WipeAt + 104 * 7 / 50.0 + 2, screen.StrikesAt, 6); // 14.56 s on a PAL machine
    }

    [Fact]
    public void EachSlot_IsStruck_InTurn_ItsFigureInItsStatusColour()
    {
        var screen = Screen(Ending(), Scene(true, false));
        var s = screen.StrikesAt;
        Assert.Equal((true, 0), (screen.At(s + 0.1).Strike, screen.At(s + 0.1).Figures)); // the hand, the thunder
        Assert.Equal((true, 1), (screen.At(s + 0.6).Strike, screen.At(s + 0.6).Figures)); // the figure, the hand still on
        Assert.Equal((false, 1), (screen.At(s + 0.9).Strike, screen.At(s + 0.9).Figures)); // TD = 0.7 with the hand off
        Assert.Equal((true, 1), (screen.At(s + 1.6).Strike, screen.At(s + 1.6).Figures));
        Assert.Equal(2, screen.At(s + 2.1).Figures);
        Assert.Equal(7, screen.FigureColour(0)); // OK: yellow
        Assert.Equal(15, screen.FigureColour(1)); // not OK: light grey (P(18) = 0)
        Assert.Equal(s + 2 * 1.5, screen.SmilesAt, 6);
    }

    [Fact]
    public void Speech_IsTypedOnStackedRows_WordByWord_TheFaceTalking()
    {
        var screen = Screen(Ending(), Scene(true));
        var smiles = screen.At(screen.SmilesAt + 0.01);
        Assert.Contains(smiles.Text, r => r is { Row: 24, Col: 8, Text: "SMILES" });
        Assert.Equal(0, smiles.Face);
        Assert.Equal(1, screen.At(screen.SmilesAt + 0.21).Face); // 10 frames at 50 Hz each
        Assert.Equal(0, screen.At(screen.SmilesAt + 1.61).Face); // eight frames, then round again
        Assert.Equal(-1, smiles.Hand);
        Assert.Equal(screen.SmilesAt + 3, screen.SpeechAt, 6);
        // "AB CD": "AB " at once, then the click 16 + 0.5 * 8 = 20 jiffies before "CD".
        var first = screen.At(screen.SpeechAt + 0.001);
        Assert.Contains(first.Text, r => r is { Row: 18, Col: 1, Text: "AB " });
        Assert.DoesNotContain(first.Text, r => r.Row == 24);
        Assert.Contains(screen.At(screen.SpeechAt + 20 / 60.0 + 0.001).Text, r => r is { Row: 18, Text: "AB CD" });
        // The line's end: the last RND * 8 (4) and a new one (4): 8 jiffies before "EF" on row 19.
        Assert.DoesNotContain(screen.At(screen.SpeechAt + 27 / 60.0).Text, r => r.Row == 19);
        var both = screen.At(screen.SpeechAt + 28.5 / 60.0);
        Assert.Contains(both.Text, r => r is { Row: 18, Text: "AB CD" });
        Assert.Contains(both.Text, r => r is { Row: 19, Text: "EF" });
        Assert.Equal(screen.SpeechAt + 36 / 60.0, screen.GrantAt, 6); // "EF"'s end: 4 + 4 jiffies
    }

    [Fact]
    public void Grant_CyclesTheHand_TheFaceAtRest_ThenDone()
    {
        var screen = Screen(Ending(), Scene(true));
        var g = screen.At(screen.GrantAt + 0.01);
        Assert.Equal((0, 0), (g.Face, g.Hand));
        Assert.Equal(1, screen.At(screen.GrantAt + 0.21).Hand);
        Assert.Equal(0, screen.At(screen.GrantAt + 1.21).Hand); // six frames
        Assert.False(screen.At(screen.DoneAt - 0.01).Done);
        Assert.True(screen.At(screen.DoneAt).Done);
        Assert.Equal(screen.GrantAt + 12, screen.DoneAt, 6);
    }

    [Fact]
    public void Overheads_LengthenTheirSteps()
    {
        var screen = Screen(Ending(overhead: 0.01, letter: 0.02), Scene(true));
        Assert.Equal(2 + 91 * 0.06 + 2, screen.WipeAt, 6);
        Assert.Contains(screen.At(screen.SpeechAt + 0.021).Text, r => r is { Row: 18, Text: "A" });
    }

    private static C64DefeatLayout Defeat() => new()
    {
        Temple = "defeat.temple", Ghost = "defeat.ghost", Door = "defeat.door", Face = "defeat.face",
        Defeated = new C64TextLine("core.defeat.your_party_has_been_defeated", 6, 23, 2, 2),
        TextCol = 1, TextRow = 23, AstralColour = 6, DemonColour = 6, RowX2 = 172, RowGap = 35, RowY = 120,
        GhostSeconds = 0.3, AfterGhostsSeconds = 2, DoorX = 124, DoorY = 27, DoorSteps = 78, DoorStepSeconds = 0.1,
        FaceX = 124, FaceY = 31, DemonSeconds = 4, FateSoundSeconds = 1.08, FateSeconds = 2, EndSeconds = 6,
    };

    private static PartyDefeatResult Fates(params DefeatFate[] fates) => new()
    {
        Fates = [.. fates.Select((f, i) => (new Character { Name = $"M{i}" }, f))],
    };

    [Theory]
    [InlineData(1, new[] { 86 })] // VIC $6E
    [InlineData(2, new[] { 68, 103 })] // VIC $5C, $7F
    [InlineData(5, new[] { 16, 51, 86, 121, 156 })] // VIC $28 + 35 k
    [InlineData(6, new[] { -2, 33, 68, 103, 138, 173 })]
    public void Ghosts_AreCentred_ByCEMETARY(int members, int[] xs) =>
        Assert.Equal(xs, C64DefeatScreen.GhostXs(members, Defeat()));

    [Fact]
    public void Defeat_Ghosts_OneByOne_ThenTheDoorRises_ThenTheFace()
    {
        var lines = new[] { "ASTRAL", "DEMON", "F0", "F1" };
        var screen = new C64DefeatScreen(Defeat(), Fates(DefeatFate.Undead, DefeatFate.Destroyed), lines, "DEFEATED");
        var f = screen.At(1);
        Assert.False(f.Temple);
        Assert.Contains(f.Text, r => r is { Row: 23, Col: 6, Text: "DEFEATED", Colour: 2 });
        Assert.Equal(0, screen.At(2.1).Ghosts);
        Assert.Contains(screen.At(2.1).Text, r => r is { Text: "ASTRAL", Colour: 6 });
        Assert.Equal(1, screen.At(2.31).Ghosts);
        Assert.Equal(2, screen.At(2.61).Ghosts);
        Assert.Equal(2 + 0.6 + 2, screen.DoorAt, 6);
        Assert.Equal(27, screen.At(screen.DoorAt + 0.05).DoorY); // the first step keeps y 77
        Assert.Equal(26, screen.At(screen.DoorAt + 0.15).DoorY);
        Assert.Equal(27 - 77, screen.At(screen.FaceAt - 0.01).DoorY); // VIC y 0
        Assert.Equal(screen.DoorAt + 7.8, screen.FaceAt, 6);
        var face = screen.At(screen.FaceAt + 0.01);
        Assert.True(face.Face);
        Assert.Contains(face.Text, r => r.Text == "DEMON");
    }

    [Fact]
    public void Defeat_EachFate_AfterItsSound_InItsColour()
    {
        var lines = new[] { "ASTRAL", "DEMON", "F0", "F1", "F2" };
        var screen = new C64DefeatScreen(Defeat(),
            Fates(DefeatFate.Undead, DefeatFate.Destroyed, DefeatFate.Resurrected), lines, "DEFEATED");
        var t = screen.FatesAt;
        Assert.Equal(screen.FaceAt + 4, t, 6);
        var sound = screen.At(t + 0.5);
        Assert.Equal(0, sound.Fates);
        Assert.DoesNotContain(sound.Text, r => r.Row == 23 && r.Text == "DEMON"); // the rows cleared
        var first = screen.At(t + 1.1);
        Assert.Equal(1, first.Fates);
        Assert.Contains(first.Text, r => r is { Text: "F0", Colour: 3 }); // GRANTED UNDEAD STATUS: cyan
        Assert.Contains(screen.At(t + 3.08 + 1.1).Text, r => r is { Text: "F1", Colour: 11 }); // DESTROYED: dark grey
        Assert.Contains(screen.At(t + 2 * 3.08 + 1.1).Text, r => r is { Text: "F2", Colour: 7 });
        Assert.Equal(3, screen.At(t + 3 * 3.08 + 1).Fates);
        Assert.Equal(t + 3 * 3.08 + 6, screen.DoneAt, 6);
        Assert.True(screen.At(screen.DoneAt).Done);
    }

    [OriginalDataFact]
    public void Packs_CarryTheC64Steps_AndTheirPictures()
    {
        var p1 = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("c64")!;
        var c64 = p1.EndingScene!.C64!;
        Assert.All(c64.Face.Concat(c64.Hand).Append(c64.Cloud).Append(c64.StrikeHand), n => Assert.NotNull(p1.Resolve(n)));
        Assert.EndsWith("ending/sprites/c6-face-0.png", p1.Resolve(c64.Face[0]));
        Assert.EndsWith("ending/sprites/c5-hand-5.png", p1.Resolve(c64.Hand[5]));
        foreach (var path in new[] { GameDataRepository.DefaultPhantasie1Path, GameDataRepository.DefaultPathFor(GameId.P2) })
        {
            var pack = GraphicsPackCatalog.Load(path).Find("c64")!;
            var d = pack.C64Defeat!;
            Assert.All(new[] { d.Temple, d.Ghost, d.Door, d.Face }, n => Assert.NotNull(pack.Resolve(n)));
            Assert.EndsWith("defeat/door.png", pack.Resolve(d.Door));
        }
        var p2 = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("c64")!.EndingScene!;
        Assert.Null(p2.C64);
        Assert.Equal(10, p2.PresentColour);
        Assert.True(p2.RowFromStart);
    }

    [OriginalDataFact]
    public void P2_WIN_PresentsEachMember_InLightRed_InItsPlace()
    {
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("c64")!.EndingScene!;
        var scene = new VictoryScene([new(Race.Human, true), new(Race.Elf, true)], ["w1"])
        {
            IntroPage = ["i1"], HeaderLines = ["speaks"], Titles = ["t0", "t1"], ClosingPage = ["c1"],
        };
        var screen = new VictoryScreen(layout, scene);
        var intro = screen.At(0.01);
        Assert.Equal(VictoryScreen.EndingPageKind.Intro, intro.Page);
        // WIN's intro page reads no key: a key does not end it.
        screen.Key(0.02);
        Assert.Equal(VictoryScreen.EndingPageKind.Intro, screen.At(0.03).Page);
        Assert.Equal(layout.IntroPage!.Seconds!.Value, screen.FadeInAt, 6);
        var t = screen.GhostsAt + 0.001; // the first title
        var first = screen.At(t);
        Assert.Equal(("t0", 0, -1), (first.Text, first.Presented, first.Stage)); // no stage: presented in the row
        var later = screen.At(screen.SpeechAt + 0.001);
        Assert.Equal(-1, later.Presented);
    }
}
