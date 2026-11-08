using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T3: Phantasie II as a playable campaign (<see cref="Phantasie2Campaign"/>) on the shipped
/// <c>GameData/phantasie2</c>: start, Guild, wilderness, dungeon, the encounter rules of <c>encounters.md</c>, the towns of
/// <c>towns.md</c> with the s81 decisions (D2-030/031/032/040) and the endgame chain of <c>endgame.md</c> (oracle:
/// <c>parity/endgame-live.md</c>).
/// </summary>
public class Phantasie2CampaignTests
{
    private static readonly GameDataRepository Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    /// <summary>Replays DOS words (0..32767) in order, then repeats the last; <c>Next(0, n)</c> clamps the word.</summary>
    private sealed class Words(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);
        private int _last;

        public int Taken { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Taken++;
            if (_q.Count > 0)
                _last = _q.Dequeue();
            return Math.Clamp(_last, minInclusive, maxExclusive - 1);
        }

        public int NextDosWord() => Next(0, 32768);

        public void Then(params int[] values)
        {
            foreach (var v in values)
                _q.Enqueue(v);
        }
    }

    /// <summary>The DOS word that gives <c>rnd = word / 327 = r</c>.</summary>
    private static int W(int r) => r * 327;

    private static GameSession NewGame(IDosRandomSource? rng = null, GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(Data, rng ?? new SeededDosRandomSource(7));
        s.StartNewGame(options);
        return s;
    }

    private static GameSession WithParty(IDosRandomSource? rng = null, GameOptions? options = null, int members = 4)
    {
        var s = NewGame(rng, options);
        var classes = new[] { CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief,
            CharacterClass.Ranger, CharacterClass.Monk };
        var races = new[] { Race.Human, Race.Dwarf, Race.Elf, Race.Halfling, Race.Gnome, Race.Human };
        s.OpenGuild();
        for (var i = 0; i < members; i++)
        {
            var c = s.CreateAndEnroll("HERO" + i, races[i], classes[i]);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        s.LeaveGuild();
        return s;
    }

    // ==== Start and loading =====================================================================================

    [OriginalDataFact]
    public void NewGame_StartsInPippacott_WithTheIICampaign()
    {
        var s = NewGame();
        Assert.Same(Phantasie2Campaign.Instance, s.Campaign);
        Assert.Equal(GameId.P2, s.Game);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Equal("PIPPACOTT", s.CurrentTown!.Name);
        Assert.True(Campaigns.IsBuilt(GameId.P2));
    }

    [OriginalDataFact]
    public void Repository_LoadsII_WithManifestCounts_AndOptionalFiles()
    {
        Assert.Equal(GameId.P2, Data.Game);
        Assert.Equal(56, Data.Spells.Count);
        Assert.Equal(16, Data.Names.Races.Count);
        Assert.Equal(17, Data.OverworldRegions.Count);
        Assert.Equal(8, Data.Dungeons.Count);
        Assert.Empty(Data.GuildRoster.Characters);
        Assert.Equal([4, 1, 2, 3, 3, 4, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1], Data.WildernessEncounters.PairTable);
        var e = Data.Rules.Encounters!;
        Assert.Equal(new EncounterRules(TerrainRoll.Rand327, true, EncounterGroupFit.SpriteHeight, 12, false, null, 0, true), e);
        Assert.Null(GameDataRepository.LoadPhantasie1().Rules.Encounters); // I keeps the DOS defaults
    }

    [OriginalDataFact]
    public void NewMember_GetsOriginByPlane_AndLeavingTown_UsesTheTownsExit()
    {
        var s = WithParty();
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie2Origin.Ferronrah, m.Phantasie2!.Origin));
        Assert.True(s.LeaveTown());
        Assert.Equal((2, 16, 3), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y)); // PIPPACOTT's exit (towns.json)
    }

    [Fact]
    public void Origin_IsNetherworld_ForTownsPastSix() =>
        Assert.Equal([Phantasie2Origin.Ferronrah, Phantasie2Origin.Ferronrah, Phantasie2Origin.Netherworld],
            new[] { 1, 6, 7 }.Select(Phantasie2Campaign.OriginFor));

    /// <summary>A long random walk through the wilderness and DNG1, answering every prompt: nothing throws.</summary>
    [OriginalDataTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomPlay_WildernessAndDungeon_RunsWithoutErrors(int seed)
    {
        var rng = new SeededDosRandomSource(seed);
        var s = WithParty(rng, members: 6);
        foreach (var m in s.Party.Members)
            m.HpCur = m.HpMax = 500; // keep the party alive long enough to see many fights
        Assert.True(s.LeaveTown());
        var dirs = new[] { CardinalDirection.North, CardinalDirection.East, CardinalDirection.South, CardinalDirection.West };
        var fights = 0;
        for (var step = 0; step < 400; step++)
        {
            Settle(s, ref fights);
            if (s.Mode == GameMode.Town && !s.LeaveTown())
                break;
            if (s.Mode == GameMode.Overworld)
                s.MoveOverworld(dirs[rng.Next(0, 4)]);
            else if (s.Mode == GameMode.Dungeon)
                s.MoveDungeon(dirs[rng.Next(0, 4)]);
        }
        Settle(s, ref fights);
        // The dungeon: DNG1 from its entrance, stepping around.
        if (s.Mode != GameMode.Overworld)
            return;
        s.EnterDungeon(1, 1, 1);
        for (var step = 0; step < 300 && s.Mode != GameMode.Town; step++)
        {
            Settle(s, ref fights);
            if (s.Mode == GameMode.Dungeon)
                s.MoveDungeon(dirs[rng.Next(0, 4)]);
            else if (s.Mode == GameMode.Overworld)
                break;
        }
        Settle(s, ref fights);
        Assert.True(fights > 0);
    }

    private static void Settle(GameSession s, ref int fights)
    {
        for (var guard = 0; guard < 50; guard++)
        {
            if (s.PartyDefeatPending)
            {
                s.ResolvePartyDefeat();
                continue;
            }
            if (s.Mode == GameMode.Combat)
            {
                fights++;
                s.FightToEnd();
                continue;
            }
            if (s.PendingTreasure.Count > 0)
            {
                s.AnswerTreasure(true);
                continue;
            }
            if (s.WildernessPrompt is { } wp)
            {
                s.AnswerWildernessPrompt(wp.Menu == WildernessMenu.SleepingMonsters ? WildernessAnswer.Attack : WildernessAnswer.Fight);
                continue;
            }
            if (s.EntryPrompt is not null)
            {
                s.AnswerOverworldEntry(false);
                continue;
            }
            if (s.DungeonEventPrompt is not null)
            {
                s.AnswerDungeonEvent(1);
                continue;
            }
            if (s.DungeonTrapPrompt is not null)
            {
                s.AnswerDungeonTrap(null);
                continue;
            }
            if (s.DungeonLockPrompt is not null)
            {
                s.AnswerDungeonLock(true);
                continue;
            }
            if (s.DungeonExitPending)
            {
                s.AnswerDungeonExit(true, false);
                continue;
            }
            return;
        }
    }

    // ==== Endgame chain on the real DNG8 (oracle: parity/endgame-live.md) ===========================================

    /// <summary>A party in DNG8 one cell north of the Ice Dragon (32,33), every member at <paramref name="score"/>.</summary>
    [OriginalDataFact]
    public void Ending_DeadMemberInTheMiddle_GetsATitle_LastOkMemberNone()
    {
        // 0x12f2a loops over records 0..n−1 (n = OK members), whatever their status (endgame.md §7 step 3, §7.1).
        var s = WithParty(members: 3);
        var members = s.Party.Members;
        members[1].Status = CharacterStatus.Dead;
        members[1].HpCur = 0;
        Phantasie2Campaign.ResolveEnding(s);
        var scene = Assert.IsType<VictoryScene>(s.PendingVictoryScene);
        Assert.Equal(2, scene.Titles.Count);
        Assert.StartsWith(members[0].Name + " - the ", scene.Titles[0]);
        Assert.StartsWith(members[1].Name + " - the ", scene.Titles[1]); // the dead one
        Assert.Equal([true, false, true], scene.Slots.Select(v => v.Granted));
        Assert.Equal(2, s.Sack.Items.Count(i => i == Phantasie2Campaign.FilmonWandId)); // wands only to OK members
    }

    [OriginalDataFact]
    public void Ending_RecordsTheOrbDeed_ForEveryMember_InTheSavedRoster()
    {
        // s149 remaster record: IV's Hall reads "destroyed II's orb" as II completed (ancestry.md section 8.4).
        var s = WithParty(members: 2);
        s.Party.Members[1].Status = CharacterStatus.Dead;
        Phantasie2Campaign.ResolveEnding(s);
        Phantasie2Campaign.ResolveEnding(s); // a second ceremony adds no second deed
        var orb = new Deed(Deed.Destroyed, new ContentId(GameId.P2, Phantasie2Campaign.OrbId));
        Assert.All(s.Party.Members, m => Assert.Equal([orb], m.Deeds!));
        Assert.All(s.VictoryGuildWrite!.OfType<Character>().Where(c => s.Party.Members.Any(m => m.Name == c.Name)),
            c => Assert.Equal([orb], c.Deeds!));
        Assert.Equal(orb, P4Hall.CompletedII);
    }

    private static GameSession AtTheDragon(int score, bool lastDead = true)
    {
        var s = WithParty(members: 5);
        foreach (var m in s.Party.Members)
            m.P2.QuestScore = score;
        if (lastDead)
        {
            s.Party.Members[^1].Status = CharacterStatus.Dead;
            s.Party.Members[^1].HpCur = 0;
        }
        Assert.True(s.LeaveTown());
        s.EnterDungeon(8, 32, 32);
        var move = s.MoveDungeon(CardinalDirection.South);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, move.Kind);
        Assert.Equal(["LEAVE", "APPROACH"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
        return s;
    }

    [OriginalDataFact]
    public void Dragon_AtScoreZero_IsTooTerrifying_AndPushesBack()
    {
        var s = AtTheDragon(0);
        s.AnswerDungeonEvent(2); // APPROACH
        Assert.Contains(s.Log, l => l.Contains("TOO TERRIFIED"));
        Assert.Equal((32, 32), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.Null(s.DungeonEventPrompt);
    }

    [OriginalDataFact]
    public void Dragon_FeedTheOrb_RunsTheFilmonEnding()
    {
        var s = AtTheDragon(Phantasie2Campaign.DragonGateScore);
        s.AnswerDungeonEvent(2); // APPROACH: SCORE >= 100 passes, >= 101 fails → GOTO EVENT 2
        Assert.Equal(["ATTACK", "TALK", "FEED IT"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
        s.AnswerDungeonEvent(2); // TALK: a line, then back to event 2
        Assert.Equal(["ATTACK", "TALK", "FEED IT"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
        s.AnswerDungeonEvent(3); // FEED IT → event 3
        Assert.Equal(["GOLD", "THE ORB", "MEMBER"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
        s.AnswerDungeonEvent(2); // THE ORB → SET SCORE 200, ENDING

        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Null(s.Dungeon);
        Assert.Equal(EndgameOutcome.Victory, s.LastEndgame);
        var members = s.Party.Members;
        Assert.All(members.Take(4), m => Assert.Equal(Phantasie2Campaign.OrbDestroyedScore, m.P2.QuestScore));
        Assert.Equal(Phantasie2Campaign.DragonGateScore, members[^1].P2.QuestScore); // the dead member keeps 100
        Assert.Equal(4, s.Sack.Items.Count(i => i == Phantasie2Campaign.FilmonWandId)); // one wand per OK member
        // The ceremony screen (endgame.md §7.1) waits for the front end: titles for records 0..n−1, n = OK members.
        var scene = Assert.IsType<VictoryScene>(s.PendingVictoryScene);
        Assert.Equal(10, scene.IntroPage.Count);
        Assert.Equal(21, scene.ClosingPage.Count);
        Assert.EndsWith("The end.", scene.ClosingPage[^1]);
        Assert.Equal("Presenting the heroes of Ferronrah", scene.HeaderLines[1]);
        Assert.Equal(4, scene.Titles.Count);
        Assert.All(scene.Titles.Select((t, i) => (t, i)), x => Assert.StartsWith(members[x.i].Name + " - the ", x.t));
        Assert.All(scene.Titles, t => Assert.EndsWith(" from Ferronrah", t));
        Assert.Equal(members.Select(m => m.Status == CharacterStatus.Ok), scene.Slots.Select(v => v.Granted));
        Assert.Equal(["They will each receive a wand of", "Filmon which has great power but", "can  only  be  used  once ."],
            scene.SpeechLines.Skip(1).Take(3));
        var prompts = new SessionPrompts(s);
        Assert.Equal(SessionPromptKind.VictoryScene, prompts.Current!.Kind);
        Assert.Equal(scene.Lines, prompts.Current.Lines); // without a pack scene the box shows every line
        Assert.Equal(10 + 2 + 4 + 5 + 21, prompts.Current.Lines.Count);
        prompts.Answer(1);
        Assert.Null(s.PendingVictoryScene);
    }

    [OriginalDataFact]
    public void Dragon_Attack_RewritesTheCell_AndASavedMapKeepsTheDragonGone()
    {
        // live s82 (parity/encounters-endgame-live.md): ATTACK makes (32,33) 0x8e and fights PLUTO'S ICE DRAGON; after a
        // map save the re-entered level has no dragon ("ICE CAVERN" only); DNG8 loaded from its own file has it again.
        var s = AtTheDragon(Phantasie2Campaign.DragonGateScore);
        s.AnswerDungeonEvent(2); // APPROACH
        s.AnswerDungeonEvent(1); // ATTACK
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(78, s.Combat!.FirstMonsterId);
        Assert.Equal(0x8e, s.Dungeon!.Level!.Get(32, 33));
        var saved = new DungeonSaveSlot(8, s.Dungeon.Level.ToBuffer());

        var again = WithParty(members: 5);
        foreach (var m in again.Party.Members)
            m.P2.QuestScore = Phantasie2Campaign.DragonGateScore;
        Assert.True(again.LeaveTown());
        again.DungeonSave = saved;
        again.EnterDungeon(8, 32, 32);
        again.MoveDungeon(CardinalDirection.South);
        Assert.Equal((32, 33), (again.Dungeon!.X, again.Dungeon.Y));
        Assert.Null(again.DungeonEventPrompt);
        Assert.NotEqual(GameMode.Combat, again.Mode);
    }

    [OriginalDataFact]
    public void Dragon_KilledAfterAttack_WinsNothing()
    {
        // live s83 (parity/encounters-endgame-live.md §8): a TOSS kills PLUTO'S ICE DRAGON; an ordinary victory (the ST's
        // "`core.combat.each_survivor_gets` 50000 XP AND 0 GP" share display is D2-043, decided s84 as the option `p2.combat-shares`; the remaster shows the manual's totals)
        // and its treasure, but no ending: SCORE stays 100, the party stays in DNG8 and (32,33) is the plain "ICE CAVERN".
        var s = AtTheDragon(Phantasie2Campaign.DragonGateScore, lastDead: false);
        foreach (var m in s.Party.Members)
            m.HpCur = m.HpMax = 2000;
        s.AnswerDungeonEvent(2); // APPROACH
        s.AnswerDungeonEvent(1); // ATTACK
        var enc = s.Combat!;
        Assert.Equal(78, enc.FirstMonsterId);
        foreach (var mon in enc.AllMonsters)
            (mon.HitPoints, mon.Defense) = (1, 0); // test power (live: TOSS 100 against HP 1)
        s.FightToEnd();

        Assert.Contains(s.Log, l => l.Contains("ICE DRAGON DIES"));
        Assert.Null(s.LastEndgame);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie2Campaign.DragonGateScore, m.P2.QuestScore));
        Assert.Equal(8, s.Dungeon!.LevelId);
        Assert.Equal(0x8e, s.Dungeon.Level!.Get(32, 33));
    }

    [OriginalDataFact]
    public void Mystic_ScoresTheOrbAndRunes_AndRaisesEveryMembersScore()
    {
        var s = WithParty(members: 3);
        var a = s.Party.Members[0];
        a.InventoryItemIds.Add(Phantasie2Campaign.OrbId);
        for (var k = 0; k < 8; k++)
            s.Party.Members[k % 2].P2.BeastRunes[k] = true;
        s.Party.Members[2].Status = CharacterStatus.Dead;
        var r = s.ConsultMystic();
        Assert.Equal("Your score is  100. You are ready to destroy the orb.", r.Message);
        Assert.All(s.Party.Members, m => Assert.Equal(100, m.P2.QuestScore)); // the dead one too

        Assert.Equal((60, 4, false), Phantasie2Campaign.MysticScore(new[] { new Character { Phantasie2 = new() { BeastRunes = [true, true, true, true, true, true, false, false] } } }));
        Assert.Equal((20, 1, true), Phantasie2Campaign.MysticScore(new[] { Carrier(score: 120) }));
        Assert.Equal(5, Phantasie2Campaign.MysticScore(new[] { new Character { Phantasie2 = new() { QuestScore = 200 } } }).Rank);

        static Character Carrier(int score)
        {
            var c = new Character { Score = score };
            c.InventoryItemIds.Add(Phantasie2Campaign.OrbId);
            return c;
        }
    }

    [OriginalDataFact]
    public void Wand_OfFilmon_BumpsStats_OnlyAtScore200()
    {
        var s = WithParty(members: 2);
        var (glade, yoshi) = (s.Party.Members[0], s.Party.Members[1]);
        glade.Stats = new CharacterStats(13, 19, 14, 14, 5, 8);
        yoshi.Stats = new CharacterStats(18, 10, 18, 11, 11, 13);
        foreach (var m in new[] { glade, yoshi })
        {
            m.P2.QuestScore = 200;
            m.InventoryItemIds.Add(Phantasie2Campaign.FilmonWandId);
            Assert.Empty(s.ItemUseOptions(m, Phantasie2Campaign.FilmonWandId)!);
            Assert.Equal("You are improved", s.UseCampaignItem(m, Phantasie2Campaign.FilmonWandId).Message);
            Assert.Equal(250, m.P2.QuestScore);
            Assert.DoesNotContain(Phantasie2Campaign.FilmonWandId, m.InventoryItemIds);
        }
        Assert.Equal(new CharacterStats(14, 20, 15, 15, 7, 8), glade.Stats);   // endgame-live.md, GLADE
        Assert.Equal(new CharacterStats(19, 12, 19, 13, 13, 13), yoshi.Stats); // YOSHI
        glade.InventoryItemIds.Add(Phantasie2Campaign.FilmonWandId);
        Assert.True(s.UseCampaignItem(glade, Phantasie2Campaign.FilmonWandId).Unusable); // SCORE 250 now
    }

    [OriginalDataFact]
    public void Amulet_Examine_GivesTheRune_ToMembersWhoSawTheKill()
    {
        var s = WithParty(members: 3);
        var amulet = Phantasie2Campaign.AmuletBase + 3;
        s.Party.Members[0].P2.MinionKilled[2] = true;
        s.Party.Members[1].P2.MinionKilled[2] = true;
        Assert.Equal(["EXAMINE", "WEAR", "LEAVE"], s.ItemUseOptions(s.Party.Members[0], amulet)!);
        Assert.Equal("LOOKS NICE !", s.UseCampaignItem(s.Party.Members[0], amulet, 1).Message);
        Assert.False(s.Party.Members[0].P2.BeastRunes[2]);
        Assert.Equal("YOU ARE CHANGED", s.UseCampaignItem(s.Party.Members[0], amulet, 0).Message);
        Assert.Equal([true, true, false], s.Party.Members.Select(m => m.P2.BeastRunes[2]));
        Assert.Null(s.ItemUseOptions(s.Party.Members[0], 4)); // a shield has no campaign use
    }

    [OriginalDataFact]
    public void Minion_Killed_DropsItsAmulet_AndLaterBecomesAPlainFight()
    {
        var s = WithParty(members: 4);
        foreach (var m in s.Party.Members)
            m.HpCur = m.HpMax = 2000;
        Assert.True(s.LeaveTown());
        var enc = s.BeginWildernessCombat(17); // minion 1, PLUTO'S GIANT CONST.
        Assert.Equal(1, Phantasie2Campaign.MinionOf(enc));
        Assert.Contains(s.Log, l => l == "PLUTO'S MINION ! !");
        foreach (var mon in enc.AllMonsters)
            mon.HitPoints = 1;
        s.FightToEnd();
        var log = s.Log.ToList();
        var takes = log.FindLastIndex(l => l.EndsWith("TAKES AN AMULET"));
        Assert.True(takes > 0, string.Join("\n", s.Log.TakeLast(30)));
        Assert.Equal("PLUTO'S MINION IS KILLED !", log[takes - 1]); // live s82: KILLED above TAKES in one box
        Assert.Contains(Phantasie2Campaign.AmuletBase + 1, s.Sack.Items);
        Assert.All(s.Party.Members, m => Assert.True(m.P2.MinionKilled[0]));

        var again = s.BeginWildernessCombat(17); // killed already: a 0..35 monster instead
        Assert.Null(Phantasie2Campaign.MinionOf(again));
        Assert.InRange(again.FirstMonsterId, 0, 35);
    }

    [OriginalDataFact]
    public void Minion_DissolveWin_DropsNoAmulet()
    {
        // live s82 (parity/encounters-endgame-live.md): `core.combat.monsters_start_dissolving` ends the fight, the group's count word is not
        // -1, so combat_victory gives no amulet and sets no flag.
        var s = WithParty(members: 4);
        foreach (var m in s.Party.Members)
            m.HpCur = m.HpMax = 2000;
        Assert.True(s.LeaveTown());
        var enc = s.BeginWildernessCombat(72); // minion 3, PLUTO'S UNDEAD SNAKE
        Assert.Equal(3, Phantasie2Campaign.MinionOf(enc));
        CombatSpells.Dissolve(enc, []);
        s.FightToEnd();
        Assert.DoesNotContain(Phantasie2Campaign.AmuletBase + 3, s.Sack.Items);
        Assert.All(s.Party.Members, m => Assert.False(m.P2.MinionKilled[2]));
        Assert.DoesNotContain(s.Log, l => l.EndsWith("TAKES AN AMULET"));
    }

    [OriginalDataFact]
    public void PartyDefeat_II_KeepsTheFoundItems_ClearsGoldAndXp()
    {
        // live s82: an AMULET 3 in the sack survived a party wipe; 0x12678 clears only 0x25eb2 / 0x25eb4 / 0x25eb0.
        var s = WithParty(members: 2);
        Assert.True(s.LeaveTown());
        s.Sack.TryAdd(Phantasie2Campaign.AmuletBase + 3);
        s.Sack.Gold = 50;
        s.Sack.Experience = 70;
        s.BeginWildernessCombat(0);
        foreach (var m in s.Party.Members)
        {
            m.Level = 10; // level − 2 = 8 > any rand/0x2000: resurrected
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        s.ResolveCombatRound();
        Assert.True(s.PartyDefeatPending);
        var result = s.ResolvePartyDefeat();
        Assert.True(result.AnyRevived);
        Assert.Contains(Phantasie2Campaign.AmuletBase + 3, s.Sack.Items);
        Assert.Equal(0, s.Sack.Gold);
        Assert.Equal(0, s.Sack.Experience);
        Assert.Equal((Phantasie2Campaign.WakeRegion, Phantasie2Campaign.WakeX, Phantasie2Campaign.WakeY),
            (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataTheory]
    [InlineData("manual")]
    [InlineData("st")]
    public void D2043_Victory_PaysTotals_OrStSurvivorShares(string value)
    {
        var s = WithParty(options: GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.P2CombatShares.Id, value), members: 2);
        Assert.True(s.LeaveTown());
        var enc = s.BeginWildernessCombat(0);
        foreach (var m in enc.AllMonsters)
            m.HitPoints = 0;
        enc.RewardXp = 308;
        enc.RewardGold = 101;
        var mark = s.LogSequence;
        var (xp0, gold0) = (s.Sack.Experience, s.Sack.Gold);
        s.ResolveCombatRound();
        var lines = s.LogSince(mark);
        if (value == "st") // 2 survivors: ⌊308/20⌋·10 = 150 XP and 50 GP each (DOS I's rule, ST pooling [~])
        {
            Assert.Contains("EACH SURVIVOR GETS", lines);
            Assert.Equal((300L, 100L), (s.Sack.Experience - xp0, s.Sack.Gold - gold0));
        }
        else
        {
            Assert.Contains("308 EXPERIENCE POINTS AND 101 GOLD", lines);
            Assert.Equal((308L, 101L), (s.Sack.Experience - xp0, s.Sack.Gold - gold0));
        }
    }

    [OriginalDataFact]
    public void PartyDefeat_II_FifthDefeatOfTheRun_ShowsTheOutOfDemonsSign()
    {
        // live s82 (counter poked to 4) + 0x12de4 [C]: increment, then == 5. Only the 5th defeat shows the sign page,
        // between the banner and the judgement; the 4th and the 6th do not.
        var s = WithParty(members: 1);
        Assert.True(s.LeaveTown());
        var p = new SessionPrompts(s);
        var kinds = new List<List<SessionPromptKind>>();
        for (var n = 1; n <= 6; n++)
        {
            if (s.Mode != GameMode.Overworld)
                Assert.True(s.LeaveTown());
            var hero = s.Party.Members[0];
            hero.Level = 10; // always resurrected
            s.BeginWildernessCombat(0);
            hero.HpCur = 0;
            hero.Status = CharacterStatus.Dead;
            s.ResolveCombatRound();
            var seen = new List<SessionPromptKind>();
            while (p.Current is { } prompt)
            {
                seen.Add(prompt.Kind);
                if (prompt.Kind == SessionPromptKind.DefeatDemonSign)
                    Assert.Equal([GameText.S("core.defeat.arrived_at_astral_plane")], prompt.Lines);
                p.Answer(1);
            }
            kinds.Add(seen);
            Assert.Equal(n == Phantasie2Campaign.DemonSignDefeat, s.LastPartyDefeat!.DemonsRanOut);
        }
        Assert.Equal(6, s.PartyDefeatsThisRun);
        Assert.Equal([SessionPromptKind.PartyDefeat, SessionPromptKind.DefeatOutcome], kinds[3]);
        Assert.Equal([SessionPromptKind.PartyDefeat, SessionPromptKind.DefeatDemonSign, SessionPromptKind.DefeatOutcome], kinds[4]);
        Assert.Equal([SessionPromptKind.PartyDefeat, SessionPromptKind.DefeatOutcome], kinds[5]);
    }

    [OriginalDataFact]
    public void PartyDefeat_II_FateLines_OnePerMember()
    {
        // live s83 (parity/encounters-endgame-live.md §9): a three-member party (L15, L3, CON 0) wiped by a night fight
        // shows "GLADE YOU HAVE BEEN RESURRECTED", "YOSHI YOU HAVE BEEN `core.defeat.x_status_undead`", "FOREST YOU HAVE BEEN
        // DESTROYED" and nothing for the empty record past the party (mode 6 hands 0x12678 the last index, not the count).
        var s = WithParty(members: 3);
        Assert.True(s.LeaveTown());
        s.BeginWildernessCombat(0);
        var (a, b, c) = (s.Party.Members[0], s.Party.Members[1], s.Party.Members[2]);
        a.Level = 15; // rand/0x2000 <= 3 < 13: resurrected
        b.Level = 3;  // 1/4 resurrected, else rand/0x2aab <= 2 < 3: undead
        c.Stats = c.Stats with { Con = 0 }; // destroyed, no draw
        foreach (var m in s.Party.Members)
        {
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        s.ResolveCombatRound();
        var result = s.ResolvePartyDefeat();
        var fates = result.Log.Skip(3).ToList(); // after the banner, the Astral Plane and the Great Demon
        Assert.True(fates.Count == 3, string.Join("\n", result.Log));
        Assert.Equal("HERO0 YOU HAVE BEEN RESURRECTED", fates[0]);
        // The test process reads I's string table first (GameTextSetup); II's table holds `core.defeat.x_status_undead`.
        Assert.Contains(fates[1], new[] { "HERO1 YOU HAVE BEEN RESURRECTED", GameText.F("core.defeat.x_status_undead", "HERO1") });
        Assert.Equal("{0} YOU HAVE BEEN GRANTED UNDEAD STATUS", GameTextSetup.PhantasieII.Get("core.defeat.x_status_undead"));
        Assert.Equal("HERO2 YOU HAVE BEEN DESTROYED", fates[2]);
        Assert.Equal(2, s.Party.Members.Count);
    }

    [OriginalDataFact]
    public void DivineII_InRegion26_SummonsPlutosWyvern_ElsewhereOnlySpendsMp()
    {
        var rng = new Words(W(40));
        var s = WithParty(rng, members: 2);
        var caster = s.Party.Members[1];
        var spells = caster.SpellsKnown;
        Array.Resize(ref spells, SpellId.DivineIi + 1);
        spells[SpellId.DivineIi] = 1;
        caster.SpellsKnown = spells;
        caster.MagicCur = 3;
        Assert.Equal(1, Magic.MagicPointCost(SpellId.DivineIi)); // slot 56 → 56 % 4 + 1
        Assert.True(s.LeaveTown());

        var away = s.CastSpell(caster, SpellId.DivineIi); // region 2: nothing but the MP
        Assert.True(away.Success);
        Assert.Equal(2, caster.MagicCur);
        Assert.Equal(GameMode.Overworld, s.Mode);

        s.Overworld.RegionId = Phantasie2Campaign.DivineIiRegion;
        rng.Then(W(10), W(10), W(0), W(40)); // r1, r2, r3 < 25 → r4 < 50 → 75
        s.CastSpell(caster, SpellId.DivineIi);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(74, s.Combat!.FirstMonsterId); // 1-based 75 = PLUTO'S WYVERN, minion 5
        Assert.Equal(5, Phantasie2Campaign.MinionOf(s.Combat));
    }

    [Theory]
    // s83: the ST's FFP (0x7246..0x7282), checked in PHANT.PRG's own routines: 50·0.02 rounds to exactly 1, 100·0.02 to 2.
    [InlineData(0, 75)]
    [InlineData(49, 75)]
    [InlineData(50, 76)]
    [InlineData(99, 76)]
    [InlineData(100, 77)] // PLUTO'S GIANT WYVERN, minion 7
    public void DivineII_BeastBranch_IsTheStsFfpSum(int r4, int oneBasedId) =>
        Assert.Equal(oneBasedId, Phantasie2Campaign.DivineIiBeastMonster(r4));

    [Fact]
    public void DivineII_BaseSummon_IsTheExactFloor_ForEveryDrawPair()
    {
        for (var r1 = 0; r1 <= 100; r1++)
        for (var r2 = 0; r2 <= 100; r2++)
            Assert.Equal(r1 * r2 * 72 / 10000, Phantasie2Campaign.DivineIiBaseMonster(r1, r2));
    }

    [OriginalDataFact]
    public void DivineI_InADungeon_ADeityGivesTheLevelsAdvice_ElsewhereOnlySpendsMp()
    {
        var rng = new Words(W(40));
        var s = WithParty(rng, members: 2);
        var caster = s.Party.Members[1];
        var spells = caster.SpellsKnown;
        Array.Resize(ref spells, SpellId.DivineIi + 1);
        spells[SpellId.DivineI] = 1;
        caster.SpellsKnown = spells;
        caster.MagicCur = 4;
        Assert.True(s.LeaveTown());

        var away = s.CastSpell(caster, SpellId.DivineI); // the wilderness: nothing but the MP
        Assert.True(away.Success);
        Assert.Equal(2, caster.MagicCur); // spell 58: 2 MP

        s.EnterDungeon(1, 1, 1);
        rng.Then(W(100), W(100)); // index 4, past the table: ZEUS meanwhile (D2-044)
        s.CastSpell(caster, SpellId.DivineI);
        Assert.Equal(0, caster.MagicCur);
        Assert.Contains("ZEUS APPEARS AND GIVES YOU ADVICE", s.Log);
        Assert.Contains(s.Log, l => l.Contains("ORACLE")); // DNG1 message 105
    }

    [Fact]
    public void DivineI_Deity_IsTheExactFloor_AndOnlyBoth100ReachIndex4()
    {
        for (var r1 = 0; r1 <= 100; r1++)
        for (var r2 = 0; r2 <= 100; r2++)
        {
            Assert.Equal(r1 * r2 * 4 / 10000, Phantasie2Campaign.DivineIDeity(r1, r2));
            Assert.Equal(r1 == 100 && r2 == 100, Phantasie2Campaign.DivineIDeity(r1, r2) == 4);
        }
    }

    [OriginalDataFact]
    public void Dng3Pool_GivesEveryLivingMemberDivineII()
    {
        var s = WithParty(members: 3);
        s.Party.Members[2].Status = CharacterStatus.Dead;
        Assert.True(s.LeaveTown());
        s.EnterDungeon(3, 13, 35);
        var move = s.MoveDungeon(CardinalDirection.South); // (13,36): event 21, the pool message
        Assert.Equal(DungeonMoveKind.ScriptedEvent, move.Kind);
        Assert.Contains(s.Log, l => l.Contains("DIVINE SPELL"));
        Assert.Equal([true, true, false], s.Party.Members.Select(m => Magic.KnowsSpell(m, SpellId.DivineIi)));
    }

    // ==== Encounter rules (encounters.md) ===========================================================================

    private static Monster Mon(int id, int height, int level = 2, int reaction = 0, int next = 100) => new()
    {
        Id = id, Name = "M" + id, HpBase = 10, Level = level, ReactionModifier = reaction, SpriteWidth = 10,
        SpriteHeight = height, GroupSizeFactor = 0, NextGroupChance = next, Companions = [id + 1, id + 1],
    };

    [OriginalDataFact]
    public void Builder_II_AveragesLevelAndReaction_AndFitsOnHeight()
    {
        var records = new Dictionary<int, Func<Monster>>
        {
            [0] = () => Mon(0, 30, level: 3, reaction: -3), [1] = () => Mon(1, 30, level: 4, reaction: 2), [2] = () => Mon(2, 30, level: 8, reaction: 4),
        };
        var rules = Data.Rules.EncounterRulesOrDefault;
        var enc = EncounterBuilder.Build(new Party(), 0, id => records[id](), new Words(W(10)), rules: rules);
        Assert.Equal(3, enc.Ranks.Count);
        Assert.Equal((3 + 4 + 8) / 3, enc.MonsterLevel);
        Assert.Equal((-3 + 2 + 4) / 3, enc.ReactionSum);

        // Two 80-high sprites never fit: 83 + 83 >= 135, so the second group is dropped (no level, no companion).
        records[0] = () => Mon(0, 80, level: 5);
        records[1] = () => Mon(1, 80, level: 9);
        enc = EncounterBuilder.Build(new Party(), 0, id => records[id](), new Words(W(10)), rules: rules);
        Assert.Single(enc.Ranks);
        Assert.Equal(5, enc.MonsterLevel);

        // I's rules on the same records fit on width (10 + 3 each) and sum L.
        enc = EncounterBuilder.Build(new Party(), 0, id => records[id](), new Words(10));
        Assert.Equal(3, enc.Ranks.Count);
        Assert.Equal(5 + 9 + 8, enc.MonsterLevel);
    }

    [OriginalDataFact]
    public void Builder_II_ClampsAFirstIdOutsideTheTable_ToSkeleton()
    {
        var enc = EncounterBuilder.Build(new Party(), 85, id => Data.GetMonster(id).ToRecord(), new Words(W(99)),
            rules: Data.Rules.EncounterRulesOrDefault);
        Assert.Equal(0, enc.FirstMonsterId);
    }

    [OriginalDataFact]
    public void DayEncounter_II_DrawsNoBlackKnightsRoll()
    {
        var party = new Party();
        party.Members.Add(new Character { Name = "A", Status = CharacterStatus.Ok });
        // First draw r = 95 (> 91: I's Black Knights). II uses it as the "< 25" test instead → the pair branch.
        var rng = new Words(W(95), W(0), W(0), W(50), W(0), W(99));
        var o = WildernessEncounter.RollDay(party, 0, 0, 1, SurpriseFlags.Normal, rng, encounterRules: Data.Rules.EncounterRulesOrDefault);
        Assert.Null(o.Prompt is { Menu: WildernessMenu.BlackKnights } ? o.Prompt : null);
        Assert.NotEqual(WildernessMenu.BlackKnights, o.Prompt?.Menu);
        var dos = WildernessEncounter.RollDay(party, 0, 0, 1, SurpriseFlags.Normal, new Words(95));
        Assert.Equal(WildernessMenu.BlackKnights, dos.Prompt!.Menu);
    }

    [OriginalDataFact]
    public void NightWatch_AtR100_IsTheEmptySlotPastTheParty()
    {
        // live s83 (r forced to 100, three members): record 3 is on watch, status 0 → no line, everyone sleeps.
        var party = new Party();
        party.Members.Add(new Character { Name = "A", Status = CharacterStatus.Ok, Skills = new(0, 0, 0, 0, 0, 100, 0, 0) });
        party.Members.Add(new Character { Name = "B", Status = CharacterStatus.Ok });
        var rules = Data.Rules.EncounterRulesOrDefault;
        var roll = NightEncounter.TryRoll(party, 0, new Words(W(0), W(0), 32767), rules)!; // r = 100 → w = 2
        Assert.Equal(2, roll.WatchSlot);
        Assert.False(roll.Heard);
        Assert.Empty(NightEncounter.ApplyAfterPrep(party, roll));
        Assert.All(party.Members, m => Assert.Equal(CharacterStatus.Sleep, m.Status));
    }

    [OriginalDataFact]
    public void NightWatch_AtR100_WithSixMembersAndAGuildElemental_TheElementalWatches()
    {
        // s92 static: with a Guild elemental the party size stays 6 and the elemental is record 6 (ST 0x83e2), so r = 100
        // puts the elemental itself on watch, with its own LISTEN and name.
        var party = new Party();
        for (var i = 0; i < 6; i++)
            party.Members.Add(new Character { Name = "M" + i, Status = CharacterStatus.Ok });
        party.Elemental = new Character { Name = "AIR", Status = CharacterStatus.Ok, Skills = new(0, 0, 0, 0, 0, 100, 0, 0) };
        var heard = NightEncounter.TryRoll(party, 1, new Words(W(0), W(0), 32767, W(50)), Data.Rules.EncounterRulesOrDefault)!;
        Assert.Equal(6, heard.WatchSlot);
        Assert.True(heard.Heard); // 50 < LISTEN 100
        Assert.Equal(["AIR IS ON WATCH AND HEARS THE MONSTERS"], NightEncounter.ApplyAfterPrep(party, heard));

        party.Elemental.Skills = new(0, 0, 0, 0, 0, 0, 0, 0);
        var slept = NightEncounter.TryRoll(party, 1, new Words(W(0), W(0), 32767, W(50)), Data.Rules.EncounterRulesOrDefault)!;
        Assert.False(slept.Heard);
        Assert.Equal(["AIR IS ON WATCH"], NightEncounter.ApplyAfterPrep(party, slept));
        Assert.All(party.Members, m => Assert.Equal(CharacterStatus.Sleep, m.Status));
        Assert.Equal(CharacterStatus.Ok, party.Elemental.Status);
    }

    [OriginalDataFact]
    public void NightWatch_AtR100_WithSixMembers_ShowsTheBlankWatcherLine()
    {
        // live s82 (r forced to 100): party record 6 is on watch — status OK from the builder, no name, LISTEN 0. Everyone
        // sleeps and " IS ON WATCH" appears with a blank name.
        var party = new Party();
        for (var i = 0; i < 6; i++)
            party.Members.Add(new Character { Name = "M" + i, Status = CharacterStatus.Ok, Skills = new(0, 0, 0, 0, 0, 100, 0, 0) });
        var roll = NightEncounter.TryRoll(party, 1, new Words(W(0), W(0), 32767), Data.Rules.EncounterRulesOrDefault)!;
        Assert.Equal(6, roll.WatchSlot);
        Assert.False(roll.Heard);
        Assert.Equal([" IS ON WATCH"], NightEncounter.ApplyAfterPrep(party, roll));
        Assert.All(party.Members, m => Assert.Equal(CharacterStatus.Sleep, m.Status));
    }

    [OriginalDataFact]
    public void Wandering_II_Uses12T_AndHasNoTrolkin()
    {
        var level = Data.CreateDungeonTraversal().GetLevel(2); // DNG2, tier 1
        Assert.Equal(1, DungeonEncounters.Tier(level));
        var rules = Data.Rules.EncounterRulesOrDefault;
        // r = 0 ≤ chance 7; r/33 = 0 → 0 + 12·1 + 14 − 8 = 18 (PANTHER).
        Assert.Equal(18, DungeonEncounters.RollWandering(level, 2, 14, 7, new Words(W(0), W(0)), rules));
        Assert.Equal(0 + 14 + 1 - 8, DungeonEncounters.RollWandering(level, 2, 14, 7, new Words(0, 0))); // I: T once
        Assert.Equal(18, DungeonEncounters.RollWandering(level, 6, 14, 7, new Words(W(0), W(0), W(99)), rules)); // no DNG6 TROLKIN override
    }

    // ==== Towns: the s81 decisions (D2-030/031/032/040) ============================================================

    [OriginalDataFact]
    public void D2045_TownEntry_RunsTheArmoryChurn_WithI_sManualRule()
    {
        // ST 0xc0a8 (s95): 18 × stock[rand/274] := 0 over ids 0..119, as DOS; D-054 (I's decision, D2-045) spares armor.
        var rng = new Words();
        var s = NewGame(rng);
        s.TownExitAverageLevel = 0; // no restock draws
        s.EnterTown(2);
        var town = s.CurrentTown!;
        town.SetStock(50, 3); // a weapon
        town.SetStock(30, 3); // armor
        rng.Then(50 * ArmoryRestock.WipeDivisor, 30 * ArmoryRestock.WipeDivisor);
        s.EnterTown(2);
        Assert.Same(town, s.CurrentTown);
        Assert.Equal(0, town.GetStock(50));
        Assert.Equal(3, town.GetStock(30));
    }

    [OriginalDataFact]
    public void Transportation_ListsEveryOtherTown_OrTheStsSamePlaneList()
    {
        var s = NewGame();
        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 9, 10], s.TransportDestinations); // D2-030 manual
        var st = NewGame(options: GameOptions.ForPreset(GameId.P2).With("p2.transport-list", "st"));
        Assert.Equal([2, 3, 4, 5, 6], st.TransportDestinations);
        st.EnterTown(9);
        Assert.Equal([10], st.TransportDestinations);
    }

    [OriginalDataFact]
    public void TownInn_RefillsTheDead_UnlessTheStOptionSkipsThem()
    {
        foreach (var (opt, refilled) in new[] { ("manual", true), ("st", false) })
        {
            var s = WithParty(options: GameOptions.ForPreset(GameId.P2).With("p2.inn-dead", opt), members: 2);
            var dead = s.Party.Members[1];
            dead.Status = CharacterStatus.Dead;
            dead.HpCur = 0;
            dead.MagicCur = 0;
            s.RestAtInn();
            Assert.Equal(refilled, dead.HpCur == dead.HpMax);
            Assert.Equal(CharacterStatus.Dead, dead.Status);
        }
    }

    [OriginalDataFact]
    public void TownInn_TakesTwoWeeks_UnlessTheStOption()
    {
        var m = WithParty(members: 1);
        var before = m.Party.Members[0].AgeClock;
        m.RestAtInn();
        Assert.Equal(before + TownMenus.InnStayAgeClockUnits, m.Party.Members[0].AgeClock); // D2-032 manual
        var st = WithParty(options: GameOptions.ForPreset(GameId.P2).With("p2.town-inn-time", "st"), members: 1);
        before = st.Party.Members[0].AgeClock;
        st.RestAtInn();
        Assert.Equal(before, st.Party.Members[0].AgeClock);
    }

    [Fact]
    public void Options_D2_03x_PresetsAndAccessors()
    {
        var manual = GameOptions.ForPreset(GameId.P2);
        Assert.False(manual.SamePlaneTransport || manual.TownInnSkipsDead || manual.TownInnNoTime || manual.InnMagicPastTable
                     || manual.OriginalPathTime);
        var st = GameOptions.ForPreset(GameId.P2, OptionPreset.Dos);
        Assert.True(st.SamePlaneTransport && st.TownInnSkipsDead && st.TownInnNoTime && st.InnMagicPastTable && st.OriginalPathTime);
        Assert.False(manual.WildernessInnTravelTime || manual.SurvivorShares); // D2-042 / D2-043
        Assert.True(st.WildernessInnTravelTime && st.SurvivorShares);
        Assert.False(GameOptions.ForPreset(GameId.P2, OptionPreset.Remaster).OriginalPathTime);
    }

    // ==== Saves ====================================================================================================

    [OriginalDataFact]
    public void QuestFields_RoundTripThroughTheSave()
    {
        var s = WithParty(members: 2);
        var a = s.Party.Members[0];
        a.P2.QuestScore = 200;
        a.P2.Origin = Phantasie2Origin.Netherworld;
        a.P2.MinionKilled[4] = true;
        a.P2.BeastRunes[7] = true;
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        var back = SessionCodec.ReadJson(json);
        var b = back.RosterSlots.First(c => c?.Name == a.Name)!;
        Assert.Equal(200, b.Phantasie2!.QuestScore);
        Assert.Equal(Phantasie2Origin.Netherworld, b.Phantasie2.Origin);
        Assert.True(b.Phantasie2.MinionKilled[4]);
        Assert.True(b.Phantasie2.BeastRunes[7]);

        var t = GameSessionFactory.Create(Data, new SeededDosRandomSource(1));
        t.ApplySnapshot(back);
        Assert.Equal((2, 16, 3), t.Towns[1].ExitPosition); // II's static exit comes back from the data
    }

    [OriginalDataFact]
    public void Save_InANetherworldRegion_RoundTrips_AndKeepsWalking()
    {
        var s = WithParty(members: 2);
        Assert.True(s.LeaveTown());
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (30, 15, 6);
        var t = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal(GameMode.Overworld, t.Mode);
        Assert.Equal((30, 15, 6), (t.Overworld.RegionId, t.Overworld.X, t.Overworld.Y));
        t.MoveOverworld(CardinalDirection.North); // the OUT30 map is loaded and steppable
    }

    [OriginalDataFact]
    public void ASaveOfPhantasieI_IsRefusedByAIISession()
    {
        var p1 = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(1));
        p1.StartNewGame();
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(p1.CaptureSnapshot()));
        var ii = NewGame();
        Assert.ThrowsAny<Exception>(() => ii.ApplySnapshot(snap));
        Assert.Equal(1, ii.CurrentTownId); // nothing half-applied
    }
}
