using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// P2 (s39) front-end glue: <see cref="SessionPrompts"/> turns Core's pending questions into one modal prompt at a time
/// and answers them through the existing session API (exit pair with D-027 / D-038, event menus, defeat, inn).
/// </summary>
public class SessionPromptsTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();
    private const int FightCell = 1240; // DNG1 (19,37), as in DungeonSaveSlotTests

    private static readonly (int Dx, int Dy, CardinalDirection Dir)[] Steps =
    [
        (0, -1, CardinalDirection.South), (0, 1, CardinalDirection.North),
        (-1, 0, CardinalDirection.East), (1, 0, CardinalDirection.West),
    ];

    private static GameSession NewSession(int seed = 5)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Prompt", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        return session;
    }

    private static GameSession InDng1OnTheExit(out SessionPrompts prompts)
    {
        var s = NewSession();
        s.EnterDungeon(1, 20, 0);
        s.Dungeon!.Y = 1;
        Assert.Equal(DungeonMoveKind.ExitDungeon, s.MoveDungeon(CardinalDirection.North).Kind);
        prompts = new SessionPrompts(s);
        return s;
    }

    [OriginalDataFact]
    public void KeepIt_PhantasieI_KeepsMenu0x1bMemory_EscIsNoAnswer()
    {
        var s = NewSession();
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        snap.PendingTreasure = [1];
        snap.PendingTreasureFinders = [null];
        s.ApplySnapshot(snap);
        var p = new SessionPrompts(s).Current!;
        Assert.Equal(SessionPromptKind.Treasure, p.Kind);
        Assert.Equal(0x1b, p.MenuId); // I's KEEP IT? menu remembers its last answer (s58)
        Assert.True(p.Remembers);
        Assert.Null(p.EscapeChoice); // Esc stays the game menu outside III's keep questions
    }

    [OriginalDataFact]
    public void NothingPending_NoPrompt()
    {
        var p = new SessionPrompts(NewSession());
        Assert.Null(p.Current);
        Assert.False(p.IsOpen);
        Assert.Throws<InvalidOperationException>(() => p.Answer(true));
    }

    [OriginalDataFact]
    public void Exit_TakeItNo_StaysOnTheExitCell_NoSaveMapQuestion()
    {
        var s = InDng1OnTheExit(out var p);
        Assert.Equal(SessionPromptKind.DungeonExitTakeIt, p.Current!.Kind);
        Assert.Equal([SessionPrompts.ExitTakeItText], p.Current.Lines);
        Assert.True(p.Current.IsYesNo);

        p.Answer(false);
        Assert.Null(p.Current);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal((20, 0), (s.Dungeon!.X, s.Dungeon.Y));
    }

    [OriginalDataFact]
    public void Exit_TakeItYes_AsksSaveMap_YesWritesTheSlot()
    {
        var s = InDng1OnTheExit(out var p);
        s.Dungeon!.Level!.SetBufferByte(FightCell, 0xC8);

        p.Answer(true);
        Assert.Equal(GameMode.Dungeon, s.Mode); // nothing happens until the second answer
        Assert.Equal(SessionPromptKind.DungeonExitSaveMap, p.Current!.Kind);
        Assert.Equal([SessionPrompts.ExitSaveMapText], p.Current.Lines);

        p.Answer(true);
        Assert.Null(p.Current);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal(1, s.DungeonSave!.DungeonId);
        Assert.Equal(0xC8, s.DungeonSave.LevelBuffer[FightCell]);
        Assert.Equal((1, 9, 8), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y)); // D-038 exit record 0xd7
    }

    [OriginalDataFact]
    public void Exit_SaveMapNo_PurgesASameDungeonSlot_D027()
    {
        var s = InDng1OnTheExit(out var p);
        s.DungeonSave = new DungeonSaveSlot(1, new byte[DungeonLevel.BufferSize]);
        p.Answer(true);
        p.Answer(false);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Null(s.DungeonSave);
    }

    [OriginalDataFact]
    public void Exit_SaveMapNo_KeepsAnotherDungeonsSlot()
    {
        var s = InDng1OnTheExit(out var p);
        var other = new DungeonSaveSlot(3, new byte[DungeonLevel.BufferSize]);
        s.DungeonSave = other;
        p.Answer(true);
        p.Answer(false);
        Assert.Same(other, s.DungeonSave);
    }

    [OriginalDataFact]
    public void Exit_Dng7OneWayExit_LandsOnOut8_D038()
    {
        GameSession s = null!;
        var level = Data.CreateDungeonTraversal().GetLevel(7);
        var hit = false;
        for (var y = 0; y < DungeonLevel.Height && !hit; y++)
        for (var x = 0; x < DungeonLevel.Width && !hit; x++)
        {
            if (level.Get(x, y) != 0xd8)
                continue;
            foreach (var (dx, dy, dir) in Steps)
            {
                if (!level.InBounds(x + dx, y + dy) || TryEnter(7, x + dx, y + dy) is not { } entered)
                    continue;
                if (entered.MoveDungeon(dir).Kind != DungeonMoveKind.ExitDungeon)
                    continue;
                s = entered;
                hit = true;
                break;
            }
        }
        Assert.True(hit, "no reachable 0xd8 exit cell in DNG7");

        var p = new SessionPrompts(s);
        p.Answer(true);
        p.Answer(false);
        Assert.Equal((8, 12, 5), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataFact]
    public void Exit_ClosedElsewhere_RestartsAtTakeIt()
    {
        var s = InDng1OnTheExit(out var p);
        p.Answer(true);
        s.ExitDungeonToWilderness(); // e.g. a load or a defeat closes the pair
        Assert.Null(p.Current);
        s.EnterDungeon(1, 20, 1);
        s.MoveDungeon(CardinalDirection.North);
        Assert.Equal(SessionPromptKind.DungeonExitTakeIt, p.Current!.Kind);
    }

    /// <summary>A session standing on (x, y) of the level, or null when that cell is not walkable.</summary>
    private static GameSession? TryEnter(int levelId, int x, int y)
    {
        var s = NewSession();
        try
        {
            s.EnterDungeon(levelId, x, y);
            return s;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Find a real DNG event cell whose message is a menu and step onto it; the menu waits.</summary>
    private static GameSession OnAMenuEvent(out SessionPrompts prompts)
    {
        for (var levelId = 1; levelId <= 10; levelId++)
        {
            var level = Data.CreateDungeonTraversal().GetLevel(levelId);
            for (var y = 0; y < DungeonLevel.Height; y++)
            for (var x = 0; x < DungeonLevel.Width; x++)
            {
                var feature = level.Get(x, y);
                if (feature < 0xdd || DungeonScript.IsSpent(level, feature))
                    continue;
                var id = level.GetBufferByte(DungeonScript.EventRecordOffset(feature)) & 0x7f;
                if (!level.Messages.TryGetValue(id, out var msg) || msg.Options.Count < 2)
                    continue;
                foreach (var (dx, dy, dir) in Steps)
                {
                    if (!level.InBounds(x + dx, y + dy))
                        continue;
                    if (TryEnter(levelId, x + dx, y + dy) is not { } s)
                        continue;
                    s.MoveDungeon(dir);
                    if (s.DungeonEventPrompt is null)
                        continue;
                    prompts = new SessionPrompts(s);
                    return s;
                }
            }
        }
        prompts = null!;
        return null!;
    }

    [OriginalDataFact]
    public void EventMenu_SurvivesASave()
    {
        var s = OnAMenuEvent(out _);
        var ev = s.DungeonEventPrompt!;
        var copy = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        copy.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal((ev.Feature, ev.MessageId), (copy.DungeonEventPrompt!.Feature, copy.DungeonEventPrompt.MessageId));
        Assert.Equal(ev.Message.Options, copy.DungeonEventPrompt.Message.Options);
    }

    [OriginalDataFact]
    public void EventMenu_IsShown_WithTheMessageOptions_AndAnsweringRunsTheScript()
    {
        var s = OnAMenuEvent(out var p);
        Assert.NotNull(s); // the data has at least one live menu event
        var ev = s.DungeonEventPrompt!;
        var prompt = p.Current!;
        Assert.Equal(SessionPromptKind.DungeonEvent, prompt.Kind);
        Assert.Equal(ev.Message.Lines, prompt.Lines);
        Assert.Equal(ev.Message.Options, prompt.Options);
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Answer(prompt.Options.Count + 1));

        Assert.Equal(SessionPromptKind.DungeonEvent, p.Answer(1));
        Assert.Null(s.DungeonEventPrompt);
        Assert.NotEqual(SessionPromptKind.DungeonEvent, p.Current?.Kind ?? SessionPromptKind.None);
    }

    [OriginalDataFact]
    public void DuringCombat_NoPrompt_ThePresenterOwnsTheScreen()
    {
        var s = NewSession();
        s.EnterDungeon(1, 20, 0);
        s.BeginDungeonCombat();
        Assert.Null(new SessionPrompts(s).Current);
    }

    [OriginalDataFact]
    public void Defeat_FirstTheBanner_ThenTheFates_ThenNothing()
    {
        var s = NewSession(9);
        var hero = s.Party.Members[0];
        s.EnterDungeon(9, 15, 0);
        s.BeginDungeonCombat(Endgame.BlackLordId, SurpriseFlags.Normal);
        hero.Level = 6; // L6: always resurrected
        hero.HpCur = 0;
        hero.Status = CharacterStatus.Dead;
        s.ResolveCombatRound();
        Assert.True(s.PartyDefeatPending);

        var p = new SessionPrompts(s);
        Assert.Equal(SessionPromptKind.PartyDefeat, p.Current!.Kind);
        Assert.Equal([SessionPrompts.DefeatText], p.Current.Lines);
        Assert.Equal([SessionPrompts.Continue], p.Current.Options);

        p.Answer(1);
        Assert.False(s.PartyDefeatPending);
        Assert.Equal(SessionPromptKind.DefeatOutcome, p.Current!.Kind);
        Assert.Contains(p.Current.Lines, l => l.Contains("RESURRECTED"));
        Assert.DoesNotContain(SessionPrompts.DefeatText, p.Current.Lines);

        p.Answer(1);
        Assert.Null(p.Current);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((1, 20, 5), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    private static GameSession NextToTheInn()
    {
        var s = NewSession();
        s.Party.GoldCarried = 300;
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 6, 13); // OUT1 (7,13) = tile 41
        return s;
    }

    [OriginalDataFact]
    public void Inn_StepOntoTile41_OffersTheInn_YesStays()
    {
        var s = NextToTheInn();
        var p = new SessionPrompts(s);
        var r = s.MoveOverworld(CardinalDirection.East);
        p.NoteOverworldMove(r);
        Assert.Equal(OverworldMoveKind.WildernessInn, r.Kind);
        Assert.Equal(SessionPromptKind.WildernessInn, p.Current!.Kind);
        Assert.Equal([SessionPrompts.WildernessInnText], p.Current.Lines);

        p.Answer(true);
        Assert.Null(p.Current);
        Assert.Equal(44, s.Party.GoldCarried); // 256 paid
    }

    [OriginalDataFact]
    public void Inn_No_ClosesTheOffer_AndAStepAwayWithdrawsIt()
    {
        var s = NextToTheInn();
        var p = new SessionPrompts(s);
        p.NoteOverworldMove(s.MoveOverworld(CardinalDirection.East));
        p.Answer(false);
        Assert.Null(p.Current);
        Assert.Equal(300, s.Party.GoldCarried);

        // Offer again, then the party is moved off the tile: the offer is stale.
        p.NoteOverworldMove(new OverworldMoveResult { Kind = OverworldMoveKind.WildernessInn });
        s.Overworld.X = 6;
        Assert.Null(p.Current);
    }

    // --- "A TOWN. ENTER?" / "A DUNGEON. ENTER?" (manual p.12) ------------------------------------------------

    private static GameSession NextTo(int x, int y)
    {
        var s = NewSession();
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, x - 1, y);
        return s;
    }

    [OriginalDataFact]
    public void TownGate_AsksFirst_YesEntersPelnor()
    {
        var s = NextTo(14, 15); // OUT1 (14,15) = Pelnor
        var p = new SessionPrompts(s);
        Assert.Equal(OverworldMoveKind.EnterTown, s.MoveOverworld(CardinalDirection.East).Kind);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal(SessionPromptKind.OverworldEntry, p.Current!.Kind);
        Assert.Equal([OverworldEntryPrompt.TownText], p.Current.Lines);
        Assert.True(p.Current.IsYesNo);

        p.Answer(true);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Null(s.EntryPrompt);
        Assert.Null(p.Current);
    }

    [OriginalDataFact]
    public void TownGate_No_IsAPlainMove()
    {
        var s = NextTo(14, 15);
        var p = new SessionPrompts(s);
        var time = s.MoveOverworld(CardinalDirection.East).TimeCost;
        Assert.Throws<InvalidOperationException>(() => s.MoveOverworld(CardinalDirection.East)); // modal

        p.Answer(false);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((14, 15), (s.Overworld.X, s.Overworld.Y));
        Assert.Equal(time, s.TravelTime); // the step's time stays; no town reset
        Assert.Null(p.Current);
        s.MoveOverworld(CardinalDirection.West); // free to walk on
    }

    [OriginalDataFact]
    public void DungeonGate_No_StaysOutside_YesEntersDng1()
    {
        var s = NextTo(9, 8); // OUT1 (9,8) = DNG1
        var p = new SessionPrompts(s);
        Assert.Equal(OverworldMoveKind.EnterDungeon, s.MoveOverworld(CardinalDirection.East).Kind);
        Assert.Equal([OverworldEntryPrompt.DungeonText], p.Current!.Lines);
        p.Answer(false);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Null(s.Dungeon);

        s.MoveOverworld(CardinalDirection.West);
        s.MoveOverworld(CardinalDirection.East);
        p.Answer(true);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal(1, s.Dungeon!.LevelId);
    }

    [OriginalDataFact]
    public void EntryPrompt_SurvivesSnapshotRoundTrip()
    {
        var s = NextTo(9, 8);
        s.MoveOverworld(CardinalDirection.East);
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var restored = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        restored.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(s.EntryPrompt, restored.EntryPrompt);
        restored.AnswerOverworldEntry(enter: true);
        Assert.Equal(GameMode.Dungeon, restored.Mode);
    }

    [OriginalDataFact]
    public void Reset_ForgetsTheInnOffer_AndTheExitYes()
    {
        var s = InDng1OnTheExit(out var p);
        p.Answer(true);
        p.Reset();
        Assert.Equal(SessionPromptKind.DungeonExitTakeIt, p.Current!.Kind);
    }

    [OriginalDataFact]
    public void WildernessEncounterMenu_SurvivesASave()
    {
        // P0.4 `[~]` "trap/lock/wilderness-menu/event-menu/defeat prompts not kept in snapshots": all five are kept since
        // s41 (trap/lock DungeonCellsTests, event menu above, defeat EndgameTests); this pins the capture side of the menu.
        var s = NewSession();
        var snap = s.CaptureSnapshot();
        snap.Wilderness!.PromptMenu = WildernessMenu.BlackKnights;
        snap.Wilderness.PromptMonsterId = WildernessEncounter.BlackKnightMonsterId;
        snap.Wilderness.PromptListenerSlot = 1;
        snap.Wilderness.PromptTitle = WildernessEncounter.BlackKnightsTitle;
        s.ApplySnapshot(snap);
        var copy = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        copy.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal(s.WildernessPrompt, copy.WildernessPrompt);
    }

    [OriginalDataFact]
    public void WildernessEncounterMenu_IsShownAndAnsweredInDosOrder()
    {
        var s = NewSession();
        var p = new SessionPrompts(s);
        var snap = s.CaptureSnapshot();
        snap.Wilderness!.PromptMenu = WildernessMenu.BlackKnights;
        snap.Wilderness.PromptMonsterId = WildernessEncounter.BlackKnightMonsterId;
        snap.Wilderness.PromptTitle = WildernessEncounter.BlackKnightsTitle;
        s.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(snap)));

        Assert.Equal(SessionPromptKind.WildernessEncounter, p.Current!.Kind);
        Assert.Equal(["RUN", "FIGHT", "SURRENDER"], p.Current.Options);
        Assert.Equal(SessionPromptKind.WildernessEncounter, p.Answer(2)); // FIGHT
        Assert.Null(s.WildernessPrompt);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(SurpriseFlags.Stalled, s.Combat!.Surprise);
    }

    [OriginalDataFact]
    public void BankDeposit_AsksSharesPerMember_ThenSplitsAllCarriedGold_D049()
    {
        // Manual p.10: "split the party's gold among the individual members … how many shares each party member gets
        // (1, 2, or 3)". 900 GP, shares 2/1/1 → 450/225/225.
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        s.OpenGuild();
        var a = s.CreateAndEnroll("Ann", Race.Human, CharacterClass.Fighter);
        var b = s.CreateAndEnroll("Bob", Race.Human, CharacterClass.Fighter);
        var c = s.CreateAndEnroll("Cid", Race.Human, CharacterClass.Fighter);
        foreach (var m in new[] { a, b, c })
            Assert.True(s.TryAddToParty(m, out _));
        s.EnterTown(1);
        foreach (var m in new[] { a, b, c })
            m.GoldInBank = 0;
        c.Status = CharacterStatus.Dead;
        var p = new SessionPrompts(s);
        Assert.False(p.BeginBankDeposit()); // nothing carried
        s.Party.GoldCarried = 900;
        Assert.True(p.BeginBankDeposit());

        Assert.Equal(SessionPromptKind.BankShares, p.Current!.Kind);
        Assert.Equal(["You have: 900 gold", "Shares for: Ann"], p.Current.Lines);
        Assert.Equal(["1", "2", "3"], p.Current.Options);
        p.Answer(2); // 2 shares
        p.Answer(1); // Bob 1
        Assert.Equal(["0", "1", "2", "3"], p.Current!.Options); // the dead may get 0
        p.Answer(2); // Cid 1

        Assert.Null(p.Current);
        Assert.Equal((450, 225, 225), (a.GoldInBank, b.GoldInBank, c.GoldInBank));
        Assert.Equal(0, s.Party.GoldCarried);
    }

    [OriginalDataFact]
    public void BankDeposit_CancelBeforeTheLastMember_DepositsNothing()
    {
        // s157: III Amiga's share window has a CANCEL; the gold moves only after the last member's shares.
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        s.OpenGuild();
        var a = s.CreateAndEnroll("Ann", Race.Human, CharacterClass.Fighter);
        var b = s.CreateAndEnroll("Bob", Race.Human, CharacterClass.Fighter);
        foreach (var m in new[] { a, b })
            Assert.True(s.TryAddToParty(m, out _));
        s.EnterTown(1);
        foreach (var m in new[] { a, b })
            m.GoldInBank = 7;
        s.Party.GoldCarried = 900;
        var p = new SessionPrompts(s);
        Assert.False(p.CancelBankDeposit()); // nothing asked yet
        Assert.True(p.BeginBankDeposit());
        p.Answer(3); // Ann 3 shares
        Assert.True(p.CancelBankDeposit());
        Assert.Null(p.Current);
        Assert.Equal((7, 7, 900), (a.GoldInBank, b.GoldInBank, s.Party.GoldCarried));
    }
}
