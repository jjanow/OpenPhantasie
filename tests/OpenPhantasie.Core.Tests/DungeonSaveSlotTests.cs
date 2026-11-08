using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// One-slot dungeon save in Core (s36, OV2 0x2d46 exit prompts / 0x5929 writer / 0x1e47 re-entry) and the D-027 rule (DOS, but NO purges a same-dungeon slot).
/// </summary>
public class DungeonSaveSlotTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();
    private const int FightCell = 1240; // DNG1 (19,37) 0xCB, the s35 fixture's cleared cell

    private static GameSession NewSessionInDng1()
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Saver", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.EnterDungeon(1, 20, 0);
        return session;
    }

    /// <summary>Step off the entrance and back on: `core.prompt.an_exit_take_it` is pending.</summary>
    private static void WalkOntoExit(GameSession s)
    {
        s.Dungeon!.Y = 1; // (20,1) is a fight cell; stand there without triggering it
        var r = s.MoveDungeon(CardinalDirection.North);
        Assert.Equal(DungeonMoveKind.ExitDungeon, r.Kind);
        Assert.True(s.DungeonExitPending);
        Assert.Equal(GameMode.Dungeon, s.Mode);
    }

    [OriginalDataFact]
    public void Entry_PlaysOnACopy_TheLoadedOriginalIsNeverMutated()
    {
        var s = NewSessionInDng1();
        s.Dungeon!.Level!.SetBufferByte(FightCell, 0xC8);
        var other = NewSessionInDng1();
        Assert.Equal(0xCB, other.Dungeon!.Level!.GetBufferByte(FightCell));
    }

    [OriginalDataTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Exit_PrintsNoTextOfItsOwn_s58(bool takeIt, bool saveMap)
    {
        // DOSBox s58: only the two prompts; no "Leaving the dungeon." on the step, nothing after either answer.
        var s = NewSessionInDng1();
        var mark = s.LogSequence;
        s.Dungeon!.Y = 1;
        Assert.Null(s.MoveDungeon(CardinalDirection.North).Message);
        s.AnswerDungeonExit(takeIt, saveMap);
        Assert.Empty(s.LogSince(mark));
    }

    [OriginalDataFact]
    public void TakeItNo_StaysOnTheExitCell()
    {
        var s = NewSessionInDng1();
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: false, saveMap: false);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal((20, 0), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.Null(s.DungeonSave);
    }

    [OriginalDataFact]
    public void SaveYes_WritesTheLiveBuffer_AndReentryRestoresIt()
    {
        var s = NewSessionInDng1();
        s.Dungeon!.Level!.SetBufferByte(FightCell, 0xC8);
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: true, saveMap: true);

        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal(1, s.DungeonSave!.DungeonId);
        Assert.Equal(0xC8, s.DungeonSave.LevelBuffer[FightCell]);

        s.EnterDungeon(1, 20, 0);
        Assert.Equal(0xC8, s.Dungeon!.Level!.GetBufferByte(FightCell));
    }

    [OriginalDataFact]
    public void SaveNo_PurgesTheOlderSlotOfThisDungeon_ReentryIsTheOriginal_D027()
    {
        var s = NewSessionInDng1();
        s.Dungeon!.Level!.SetBufferByte(FightCell, 0xC8);
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: true, saveMap: true);   // older save: 0xC8

        s.EnterDungeon(1, 20, 0);
        s.Dungeon!.Level!.SetBufferByte(FightCell, 0x01);  // later progress
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: true, saveMap: false);  // NO: DOS keeps 0xC8; the remaster purges it (D-027)
        Assert.Null(s.DungeonSave);

        s.EnterDungeon(1, 20, 0);
        Assert.Equal(0xCB, s.Dungeon!.Level!.GetBufferByte(FightCell));
    }

    [OriginalDataFact]
    public void SaveNo_KeepsASlotOfAnotherDungeon_D027()
    {
        var s = NewSessionInDng1();
        var other = new DungeonSaveSlot(2, Data.Dungeons.Single(d => d.Id == "DNG1").ToRuntime().ToBuffer());
        s.DungeonSave = other;
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Same(other, s.DungeonSave);
    }

    [OriginalDataFact]
    public void SlotOfAnotherDungeon_LoadsTheOriginal()
    {
        var s = NewSessionInDng1();
        var buffer = Data.Dungeons.Single(d => d.Id == "DNG1").ToRuntime().ToBuffer();
        buffer[FightCell] = 0xC8;
        s.DungeonSave = new DungeonSaveSlot(2, buffer);
        s.ExitDungeonToWilderness();
        s.EnterDungeon(1, 20, 0);
        Assert.Equal(0xCB, s.Dungeon!.Level!.GetBufferByte(FightCell));
    }

    [OriginalDataFact]
    public void S35Fixture_LoadedAsTheSlot_RestoresTheClearedCell()
    {
        var s = NewSessionInDng1();
        s.ExitDungeonToWilderness();
        s.DungeonSave = DungeonSaveCodec.ReadFiles(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "DNG.s35.sav"),
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "DNGX.s35.sav"));
        s.EnterDungeon(1, 20, 0);
        Assert.Equal(0xC8, s.Dungeon!.Level!.Get(19, 37));
    }

    [OriginalDataFact]
    public void ExitRecords_DosTargets_D038()
    {
        DungeonLevel Level(int n) => Data.Dungeons.Single(d => d.Id == $"DNG{n}").ToRuntime();
        Assert.Equal((1, 9, 8), Level(1).ExitTarget(0xd7));
        Assert.Equal((2, 21, 7), Level(9).ExitTarget(0xd7));
        Assert.Equal((19, 20, 10), Level(10).ExitTarget(0xd7));
        Assert.Equal(0xd8, Level(7).Get(16, 26));
        Assert.Equal((8, 12, 5), Level(7).ExitTarget(0xd8)); // one-way exit, no OUT8 feature there
        Assert.Null(Level(1).ExitTarget(0x01));
    }

    [OriginalDataFact]
    public void TakeItYes_LandsOnTheExitRecordTile_D038()
    {
        var s = NewSessionInDng1();
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (5, 3, 3); // not the DNG1 entrance
        WalkOntoExit(s);
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal((1, 9, 8), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    /// <summary>Record 0 of every level must be the tile in Core's own overworld data that enters that dungeon (axis check).</summary>
    [OriginalDataFact]
    public void ExitRecord0_IsCoreEntranceTile_ForEveryLevel_D038()
    {
        var regions = Data.OverworldRegions.Select(r => r.ToRuntime()).ToDictionary(r => r.RegionId);
        for (var n = 1; n <= 10; n++)
        {
            var (region, x, y) = Data.Dungeons.Single(d => d.Id == $"DNG{n}").ToRuntime().ExitTarget(0xd7)!.Value;
            var r = regions[region];
            Assert.True(r.GetNormalized(x, y) / 10 == 5, $"DNG{n}: OUT{region} ({x},{y}) is not a dungeon tile");
            Assert.Equal(n, r.FindFeature(x, y)?.Arg);
        }

        var out8 = regions[8];
        Assert.Null(out8.FindFeature(12, 5)); // DNG7's one-way exit: no door there
        Assert.NotEqual(0, out8.GetNormalized(12, 5));
    }
}

