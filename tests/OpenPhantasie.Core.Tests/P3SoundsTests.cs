using OpenPhantasie.Data;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s136: III's sounds outside combat (<see cref="P3Sounds"/>; Amiga FORMAT.md 7.2, DOS's event word <c>FAR:0xde58</c>):
/// the wall bump, the special doors, the Damage Report, and the event ops' word: an op with a message plays it at once, an
/// op without one leaves it for the program's end, and the next op byte (an ending one too) clears it.
/// </summary>
public class P3SoundsTests
{
    /// <summary>Dungeon 5 with the party on (11, 0); (11, 1) becomes event square 0xdd running <paramref name="program"/>.</summary>
    private static (GameSession S, long Mark) WithProgram(params byte[] program)
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 11, 0);
        var level = s.Dungeon!.Level!;
        level.Set(11, 1, 0xdd);
        level.SetBufferByte(Phantasie3Campaign.EventSquares, 0); // no text, one choice
        level.SetBufferByte(Phantasie3Campaign.EventSquares + 1, 1);
        level.SetBufferByte(Phantasie3Campaign.EventSquares + 2, 0); // program 0
        for (var i = 0; i < program.Length; i++)
            level.SetBufferByte(Phantasie3DungeonScript.ProgramBase + i, program[i]);
        level.SetBufferByte(Phantasie3DungeonScript.ProgramBase + program.Length, 0); // an ending byte after it
        s.Party.GoldCarried = 10_000;
        return (s, s.SoundSequence);
    }

    private static IReadOnlyList<int> Run(byte[] program)
    {
        var (s, mark) = WithProgram(program);
        var r = s.MoveDungeon(CardinalDirection.South);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, r.Kind);
        return s.SoundsSince(mark);
    }

    private const byte More = 0x80; // the continue bit

    [OriginalDataTheory]
    [InlineData(new byte[] { 7, 1 }, new[] { 3 })] // "The party finds some gold." with 3
    [InlineData(new byte[] { 8, 1 }, new[] { 3 })] // op 8 alone and last: its word sounds at the end
    [InlineData(new byte[] { 8 | More, 1 }, new int[0])] // the ending byte's op call clears it (Amiga 0x1c9b0 / 0x1cadc)
    [InlineData(new byte[] { 9, 0 }, new[] { 42 })] // "Some gold was taken."
    [InlineData(new byte[] { 10, 0 }, new[] { 42 })]
    [InlineData(new byte[] { 12, 11, 2, 0 }, new[] { 9 })] // "The party is teleported."
    [InlineData(new byte[] { 13, 11, 2, 0 }, new int[0])] // op 13 writes no word
    [InlineData(new byte[] { 14, 0, 0, 0 }, new[] { 27 })] // "A noise is heard in the distance."
    [InlineData(new byte[] { 15, 0, 0, 0 }, new int[0])]
    [InlineData(new byte[] { 1, 0 }, new[] { 42, 42 })] // the Damage Report, then the word at the end
    [InlineData(new byte[] { 1 | More, 0, 7, 1 }, new[] { 42, 3 })] // the next op clears op 1's word
    [InlineData(new byte[] { 8 | More, 1, 9, 0 }, new[] { 42 })] // op 9's own word only
    [InlineData(new byte[] { 16, 0 }, new[] { 42 })] // op 16: the Damage Report, no word
    public void EventOps_PlayTheirWord(byte[] program, int[] expected) =>
        Assert.Equal(expected, Run(program));

    [OriginalDataFact]
    public void WallBump_Plays12_OffTheMapNothing()
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 11, 0);
        var mark = s.SoundSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, s.MoveDungeon(CardinalDirection.West).Kind); // (10, 0) is 0
        Assert.Equal([P3Sounds.Wall], s.SoundsSince(mark));
        mark = s.SoundSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, s.MoveDungeon(CardinalDirection.North).Kind); // y −1: off the map
        Assert.Empty(s.SoundsSince(mark));
    }

    [OriginalDataTheory]
    [InlineData(0xc7)]
    [InlineData(0xd6)]
    public void SpecialDoor_Plays42(byte door)
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 11, 0);
        s.Dungeon!.Level!.Set(11, 1, door);
        var mark = s.SoundSequence;
        Assert.Equal(DungeonMoveKind.BlockedWall, s.MoveDungeon(CardinalDirection.South).Kind);
        Assert.Equal([P3Sounds.Loss], s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void ArmoryRefusals_Play36_ASaleNone()
    {
        var s = WithParty();
        var town = s.CurrentTown!;
        var id = town.Stock.Select((n, i) => (n, i)).First(t => t.i > 0 && t.n > 0).i;
        var shopper = s.Party.Members[0];

        s.Party.GoldCarried = 0; // "The party cannot afford it!"
        var mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Failed, s.Buy(shopper, id).Kind);
        Assert.Equal([P3Sounds.ArmoryRefusal], s.SoundsSince(mark));
        Assert.Equal(36, P3Sounds.ArmoryRefusal);

        s.Party.GoldCarried = 1_000_000; // a full pack: "He cannot carry anymore!"
        while (!CarriedItems.IsFull(shopper))
            CarriedItems.Put(shopper, id);
        mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Failed, s.Buy(shopper, id).Kind);
        Assert.Equal([P3Sounds.ArmoryRefusal], s.SoundsSince(mark));

        var other = s.Party.Members[1]; // a sale: no sound
        mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Ok, s.Buy(other, id).Kind);
        Assert.Empty(s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void GuildNew_KeepingTheRoll_Plays6()
    {
        // s138: Amiga 0x13938 (Guild New character 0x12bfc): OK on the roll's sheet, then the name field.
        var s = WithParty();
        var mark = s.SoundSequence;
        s.KeepRolledMember();
        Assert.Equal([P3Sounds.RollKept], s.SoundsSince(mark));
        Assert.Equal(6, P3Sounds.RollKept);
    }

    [OriginalDataFact]
    public void SaveFailed_Plays12_TheDiskErrorSound()
    {
        // s138: Amiga 0x20308 in 0x20258, the "Disk Error! A save may have been attempted on a protected disk" box.
        var s = WithParty();
        var mark = s.SoundSequence;
        s.NoteSaveFailed();
        Assert.Equal([P3Sounds.DiskError], s.SoundsSince(mark));
        Assert.Equal(12, P3Sounds.DiskError);
    }

    [OriginalDataFact]
    public void ConfirmQuestion_Plays36()
    {
        // s141: Amiga 0xaae2 (window 1's confirm questions, the guild Add's "two months" among them) plays 36 first.
        var s = WithParty();
        var mark = s.SoundSequence;
        s.AskConfirmQuestion();
        Assert.Equal([P3Sounds.ConfirmQuestion], s.SoundsSince(mark));
        Assert.Equal(36, P3Sounds.ConfirmQuestion);
    }

    [OriginalDataFact]
    public void RollKeptAndSaveFailed_OtherGames_PlayNothing()
    {
        var s = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(),
            new SeededDosRandomSource(1));
        var mark = s.SoundSequence;
        s.KeepRolledMember();
        s.NoteSaveFailed();
        s.AskConfirmQuestion();
        Assert.Empty(s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void GuildLearning_Plays6_OrOnFailure42_RefusalNone()
    {
        var s = WithParty();
        var m = s.Party.Members[0];
        m.Class = CharacterClass.Wizard;
        m.Level = 2;
        m.Stats = new CharacterStats(Str: 10, Int: 15, Dex: 10, Con: 10, Cha: 20, Luck: 10);
        m.SpellsKnown[0] = 2;
        m.SpellsKnown[3] = 2;
        s.Party.GoldCarried = 1000;

        var mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Ok, s.LearnSpell(m, 0, learned: true).Kind);
        Assert.Equal([6], s.SoundsSince(mark));
        Assert.Equal(1, m.SpellsKnown[0]);

        mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Ok, s.LearnSpell(m, 3, learned: false).Kind);
        Assert.Equal([P3Sounds.Loss], s.SoundsSince(mark));
        Assert.Equal(2, m.SpellsKnown[3]);

        s.Party.GoldCarried = 0; // "can't afford": a refusal, no sound
        mark = s.SoundSequence;
        Assert.Equal(TownTransactionKind.Failed, s.LearnSpell(m, 3, learned: true).Kind);
        Assert.Empty(s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void SoundsSince_KeepsTheLast16()
    {
        var s = WithParty();
        var mark = s.SoundSequence;
        for (var i = 1; i <= 20; i++)
            s.PlaySound(i);
        s.PlaySound(0); // no sound: not counted
        Assert.Equal(mark + 20, s.SoundSequence);
        Assert.Equal(Enumerable.Range(5, 16), s.SoundsSince(mark));
        Assert.Equal([19, 20], s.SoundsSince(s.SoundSequence - 2));
    }

    [OriginalDataFact]
    public void WildernessEncounter_Plays24_OnlyWhenOneStarts()
    {
        var s = WithParty();
        Assert.True(s.LeaveTown());
        for (var i = 0; i < 400 && s.Mode == GameMode.Overworld; i++)
        {
            if (s.EntryPrompt is not null)
                s.AnswerOverworldEntry(false);
            var mark = s.SoundSequence;
            s.MoveOverworld(i % 2 == 0 ? CardinalDirection.East : CardinalDirection.West);
            var started = s.Mode == GameMode.Combat || s.WildernessPrompt is not null;
            Assert.Equal(started ? [P3Sounds.Encounter] : [], s.SoundsSince(mark).Where(n => n == P3Sounds.Encounter));
            if (started)
                return;
        }
        Assert.Fail("no wilderness encounter in 400 steps");
    }
}
