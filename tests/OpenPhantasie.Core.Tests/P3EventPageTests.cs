using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;
using static OpenPhantasie.Core.Tests.Phantasie3DungeonTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's dungeon event-text page (Amiga <c>0x1d982</c>, DOS ovl4 <c>0x1c4d</c>; <c>dungeon-events.md</c> §2.1): a text
/// with no menu waits for a key and stops its program there; the texts of one step pile up on one page; a menu's text
/// goes on the page and is answered with a number. Built on crafted event squares in dungeon 5's work record.
/// </summary>
public class P3EventPageTests
{
    /// <summary>Spare program slots and event entries for the crafted squares (the work record only).</summary>
    private const int ProgA = 200, ProgB = 210, EntryA = 30, EntryB = 31;

    private static int Entry(int e) => Phantasie3Campaign.EventSquares + 5 * e;

    private static int Prog(int p) => Phantasie3DungeonScript.ProgramOffset(p);

    private static void Bytes(DungeonLevel level, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
            level.SetBufferByte(at + i, (byte)bytes[i]);
    }

    /// <summary>Dungeon 5 with no wandering fights, and a text with no menu (index, line count).</summary>
    private static (GameSession S, DungeonLevel Level, int Text, int Lines) Setup()
    {
        var s = WithParty();
        Enter(s, 5);
        var level = s.Dungeon!.Level!;
        var (text, msg) = level.Messages
            .Where(kv => kv.Value.Options.Count == 0 && string.Concat(kv.Value.Lines).Length >= 3)
            .Select(kv => (kv.Key, kv.Value)).First();
        return (s, level, text, msg.Lines.Count);
    }

    /// <summary>A quiet floor square with a quiet floor neighbour, made into event square <paramref name="entry"/>.</summary>
    private static (int X, int Y) Square(DungeonLevel level, int entry)
    {
        // Floor by the step's ranges (ovl4 0xe14): not a wall, fight, random square, trap, door, exit or event square.
        static bool Quiet(byte b) => b is not (0 or (>= 2 and <= 6) or (>= 8 and <= 17) or (>= 0x80 and <= 0x89) or >= 0xba);
        for (var y = 1; y < level.GridHeight - 1; y++)
        for (var x = 1; x < level.GridWidth - 1; x++)
        {
            if (!Quiet(level.Get(x, y)))
                continue;
            if (!new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }.Any(d => Quiet(level.Get(x + d.Item1, y + d.Item2))))
                continue;
            level.Set(x, y, (byte)(0xdd + entry));
            return (x, y);
        }
        throw new InvalidOperationException("No quiet square.");
    }

    private static void Step(GameSession s, (int X, int Y) at)
    {
        s.Dungeon!.WanderingChance = 0;
        StepOnto(s, at.X, at.Y);
    }

    [OriginalDataFact]
    public void SquareText_WaitsForAKey_BeforeItsProgramRuns()
    {
        var (s, level, text, lines) = Setup();
        Bytes(level, Entry(EntryA), text + 1, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x08, 2); // gold + 512, no message, end
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;

        Step(s, at);
        var page = Assert.IsType<DungeonTextPage>(s.DungeonPage);
        Assert.True(page.WaitsForKey);
        Assert.Equal(lines, page.Lines.Count);
        Assert.Equal(gold, s.Sack.Gold); // the program has not run yet

        var prompt = Assert.IsType<SessionPrompt>(new SessionPrompts(s).Current);
        Assert.Equal(SessionPromptKind.DungeonPage, prompt.Kind);
        Assert.Equal(1, prompt.EscapeChoice); // any key: Esc too (Amiga 0x1e8c4(1))
        new SessionPrompts(s).Answer(1);

        Assert.Equal(gold + 512, s.Sack.Gold);
        Assert.Null(s.DungeonPage); // nothing more waits on it
    }

    [OriginalDataFact]
    public void TextOp_ThenFight_NoCombatUntilTheKey()
    {
        var (s, level, text, _) = Setup();
        Bytes(level, Entry(EntryA), 0, 1, ProgA); // no text of its own
        Bytes(level, Prog(ProgA), 0x80 | 11, text + 1, 5, 1); // op 11 text, then fight monster 1
        var at = Square(level, EntryA);

        Step(s, at);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.True(s.DungeonPage!.WaitsForKey);
        s.AcknowledgeDungeonPage();
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Null(s.DungeonPage); // the fight starts a new page (Amiga 0x1da86)
    }

    [OriginalDataFact]
    public void SubSquareTexts_PileUpOnOnePage_ThenTheCallersRestRuns()
    {
        var (s, level, text, lines) = Setup();
        Bytes(level, Entry(EntryA), 0, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x80 | 22, EntryB + 1, 0x08, 1); // run square B, then gold + 256
        Bytes(level, Entry(EntryB), text + 1, 1, ProgB);
        Bytes(level, Prog(ProgB), 0x80 | 11, text + 1, 0x08, 2); // another text, then gold + 512
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;

        Step(s, at);
        Assert.Equal(lines, s.DungeonPage!.Lines.Count); // square B's text
        s.AcknowledgeDungeonPage();
        Assert.True(s.DungeonPage!.WaitsForKey);
        Assert.Equal(2 * lines, s.DungeonPage.Lines.Count); // op 11's text under it
        Assert.Equal(gold, s.Sack.Gold);
        s.AcknowledgeDungeonPage();
        Assert.Equal(gold + 512 + 256, s.Sack.Gold); // B's rest, then the caller's rest
        Assert.Null(s.DungeonPage);
    }

    [OriginalDataFact]
    public void AKeepQuestionEndingASubSquaresMenuChoice_StillRunsTheCallersRest()
    {
        var (s, level, text, _) = Setup();
        Bytes(level, Entry(EntryA), 0, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x80 | 22, EntryB + 1, 0x08, 1); // run square B, then gold + 256
        Bytes(level, Entry(EntryB), text + 1, 2, ProgB, ProgB); // a two-choice menu
        Bytes(level, Prog(ProgB), 6, 66); // "Keep …?" and the program ends
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;

        Step(s, at);
        Assert.NotNull(s.DungeonEventPrompt);
        s.AnswerDungeonEvent(1);
        Assert.Single(s.PendingTreasure);
        s.AnswerTreasure(false);
        Assert.Equal(gold + 256, s.Sack.Gold); // the caller's rest ran after the answer
        Assert.Null(s.PendingDungeonScript);
    }

    [OriginalDataFact]
    public void ATextShorterThanThreeBytes_IsNotShown_AndDoesNotWait()
    {
        var (s, level, _, _) = Setup();
        var messages = Assert.IsType<Dictionary<int, DungeonMessage>>(level.Messages);
        const int Short = 250;
        messages[Short] = new DungeonMessage(["ab"], []);
        Bytes(level, Entry(EntryA), Short + 1, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x08, 2);
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;

        Step(s, at);
        Assert.Null(s.DungeonPage);
        Assert.Equal(gold + 512, s.Sack.Gold);
    }

    [OriginalDataFact]
    public void APageSurvivesASave_AndCarriesOnAfterTheLoad()
    {
        var (s, level, text, lines) = Setup();
        Bytes(level, Entry(EntryA), 0, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x80 | 22, EntryB + 1, 0x08, 1);
        Bytes(level, Entry(EntryB), text + 1, 1, ProgB);
        Bytes(level, Prog(ProgB), 0x08, 2);
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;
        Step(s, at);

        var t = OpenPhantasie.Data.GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(3));
        t.ApplySnapshot(OpenPhantasie.Data.SessionCodec.ReadJson(OpenPhantasie.Data.SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.True(t.DungeonPage!.WaitsForKey);
        Assert.Equal(lines, t.DungeonPage.Lines.Count);
        t.AcknowledgeDungeonPage();
        Assert.Equal(gold + 512 + 256, t.Sack.Gold); // the sub-square's program and the caller's rest
    }

    [OriginalDataFact]
    public void AStepAwayDropsThePage_AndTheStoppedProgram()
    {
        var (s, level, text, _) = Setup();
        Bytes(level, Entry(EntryA), text + 1, 1, ProgA);
        Bytes(level, Prog(ProgA), 0x08, 2);
        var at = Square(level, EntryA);
        var gold = s.Sack.Gold;
        Step(s, at);
        Assert.NotNull(s.DungeonPage);

        var d = s.Dungeon!;
        var back = (d.PrevX - d.X, d.PrevY - d.Y) switch
        {
            (0, -1) => CardinalDirection.North,
            (0, 1) => CardinalDirection.South,
            (1, 0) => CardinalDirection.East,
            _ => CardinalDirection.West,
        };
        s.MoveDungeon(back); // the front end lets the party walk off (Core drops what waited, as for every prompt)
        Assert.Null(s.DungeonPage);
        Assert.Null(s.PendingDungeonScript);
        Assert.Equal(gold, s.Sack.Gold);
    }

    [OriginalDataFact]
    public void AMenuText_GoesOnThePage_WithItsChoices_AndDoesNotWait()
    {
        var s = WithParty();
        Enter(s, 5);
        StepOnto(s, 19, 24); // the strange altar (four choices)
        var page = Assert.IsType<DungeonTextPage>(s.DungeonPage);
        Assert.False(page.WaitsForKey);
        Assert.Equal("4) Pray on it", page.Lines[^1]);
        Assert.Equal(SessionPromptKind.DungeonEvent, new SessionPrompts(s).Current!.Kind);
    }
}
