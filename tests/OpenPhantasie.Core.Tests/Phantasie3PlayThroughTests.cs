using OpenPhantasie.Data;
using Xunit.Abstractions;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T4: a scripted Phantasie III play-through on the shipped data, start → as far as the campaign goes: a new game
/// in Pendragon, the Guild and town services, the walk to the Pendragon archives (Filmon, the wand pedestal), the crystal
/// building's plane exits, the Castle of Dark's way to the Netherworld, the dark fortress and Nikademus, the gods' reward
/// back in Pendragon, a party defeat on the Astral Plane, and a save / load round trip in a dungeon. Fights run on the
/// shared resolver (III's own combat actions are an open item); where a stage needs a fight's outcome the test rigs the
/// monsters' HP rather than grinding levels.
/// </summary>
public class Phantasie3PlayThroughTests(ITestOutputHelper output)
{
    private static readonly CardinalDirection[] Directions =
        [CardinalDirection.North, CardinalDirection.South, CardinalDirection.East, CardinalDirection.West];

    private static (int Dx, int Dy) Delta(CardinalDirection d) => d switch
    {
        CardinalDirection.North => (0, -1),
        CardinalDirection.South => (0, 1),
        CardinalDirection.East => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>A party of six strong characters (stats and HP raised so the shared fights end quickly).</summary>
    private static GameSession StrongParty(int seed = 11, GameOptions? options = null)
    {
        var s = WithParty(new SeededDosRandomSource(seed), options, members: 6);
        foreach (var m in s.Party.Members)
        {
            m.Stats = m.Stats with { Str = 18, Dex = 18, Con = 18 };
            m.HpMax = m.HpCur = 5000;
            m.Skills = m.Skills with { Attack = 250, Parry = 120 };
        }
        return s;
    }

    /// <summary>Answer whatever the game waits for with the cautious default, until it waits for nothing.</summary>
    private void Settle(GameSession s, int maxSteps = 400)
    {
        for (var i = 0; i < maxSteps; i++)
        {
            if (s.PartyDefeatPending)
                s.ResolvePartyDefeat();
            else if (s.Mode == GameMode.Combat)
                s.FightToEnd();
            else if (s.PendingTreasure.Count > 0)
                s.AnswerTreasure(true);
            else if (s.DungeonPage is { WaitsForKey: true })
                s.AcknowledgeDungeonPage(); // "Press a key"
            else if (s.DungeonEventPrompt is { } ev)
                s.AnswerDungeonEvent(ev.MessageId switch
                {
                    -1 => 1, // Yes: disarm the trap / continue past the door (the fight follows)
                    -2 => s.Party.Members.FindIndex(m => m.Class == CharacterClass.Thief) + 1, // the thief disarms
                    _ => ev.Message.Options.Count, // the last choice (Leave)
                });
            else if (s.DungeonExitPending)
                s.AnswerDungeonExit(takeIt: false, saveMap: false);
            else if (s.WildernessPrompt is not null)
                s.AnswerWildernessPrompt(WildernessAnswer.Fight);
            else if (s.EntryPrompt is not null)
                s.AnswerOverworldEntry(false);
            else
                return;
        }
        throw new InvalidOperationException("The game kept asking.");
    }

    /// <summary>BFS over the current plane map: land squares and no places but the goal; water and lava only when needed.</summary>
    private static List<CardinalDirection>? OverworldPath(GameSession s, int gx, int gy) =>
        OverworldPath(s, gx, gy, wet: false) ?? OverworldPath(s, gx, gy, wet: true);

    private static List<CardinalDirection>? OverworldPath(GameSession s, int gx, int gy, bool wet)
    {
        var w = Campaign(s).World;
        var map = s.CurrentOverworldRegion;
        bool Open(int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= map.GridWidth - 1 || y >= map.GridHeight - 1)
                return false;
            var tile = map.GetRaw(x, y);
            if (w.Terrain.IsBlocked(tile))
                return false;
            if ((x, y) == (gx, gy))
                return true;
            if (w.PlaceAt(map.RegionId, x, y) is not null)
                return false;
            var t = w.Terrain.TerrainOf(tile, w.IsNetherworld(map.RegionId));
            return wet || t is not (P3Terrain.Water or P3Terrain.Lava);
        }
        return Bfs((s.Overworld.X, s.Overworld.Y), (gx, gy), Open);
    }

    private static List<CardinalDirection>? Bfs((int X, int Y) from, (int X, int Y) to, Func<int, int, bool> open)
    {
        var prev = new Dictionary<(int, int), ((int, int) From, CardinalDirection Dir)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(from);
        prev[from] = (from, CardinalDirection.North);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == to)
            {
                var path = new List<CardinalDirection>();
                for (var at = to; at != from; at = prev[at].From)
                    path.Add(prev[at].Dir);
                path.Reverse();
                return path;
            }
            foreach (var d in Directions)
            {
                var (dx, dy) = Delta(d);
                var n = (c.X + dx, c.Y + dy);
                if (!prev.ContainsKey(n) && open(n.Item1, n.Item2))
                {
                    prev[n] = (c, d);
                    queue.Enqueue(n);
                }
            }
        }
        return null;
    }

    /// <summary>Walk to (gx, gy) on the current plane; fights and questions on the way are settled. True on arrival.</summary>
    private bool WalkTo(GameSession s, int gx, int gy)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Settle(s);
            if (s.Mode != GameMode.Overworld)
            {
                output.WriteLine($"walk stopped: {s.Mode} in town {s.CurrentTownId}");
                return false;
            }
            if ((s.Overworld.X, s.Overworld.Y) == (gx, gy))
                return true;
            var path = OverworldPath(s, gx, gy) ?? throw new InvalidOperationException($"No way to ({gx}, {gy}).");
            foreach (var dir in path)
            {
                var r = s.MoveOverworld(dir);
                if ((s.Overworld.X, s.Overworld.Y) == (gx, gy))
                    return true;
                if (r.Kind == OverworldMoveKind.Blocked || s.Mode != GameMode.Overworld || s.PartyDefeatPending
                    || s.WildernessPrompt is not null || s.EntryPrompt is not null)
                    break; // settle, then plan again from where the party is
            }
        }
        return (s.Overworld.X, s.Overworld.Y) == (gx, gy);
    }

    /// <summary>Walk to dungeon cell (gx, gy): BFS over the cells not 0, avoiding other event squares and traps.</summary>
    private bool WalkInDungeon(GameSession s, int gx, int gy)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            Settle(s);
            if (s.Mode != GameMode.Dungeon)
            {
                output.WriteLine($"left the dungeon: {s.Mode}");
                return false;
            }
            var d = s.Dungeon!;
            if ((d.X, d.Y) == (gx, gy))
                return true;
            var level = d.Level!;
            bool Open(int x, int y)
            {
                if (!level.IsWalkable(x, y))
                    return false;
                if ((x, y) == (gx, gy))
                    return true;
                var c = level.Get(x, y);
                return c is not (>= 0xdd or (>= 0xd7 and <= 0xd9));
            }
            var path = Bfs((d.X, d.Y), (gx, gy), Open);
            if (path is null)
            {
                for (var yy = 0; yy < level.GridHeight; yy++)
                    output.WriteLine(string.Concat(Enumerable.Range(0, level.GridWidth).Select(xx => level.Get(xx, yy).ToString("x2") + " ")));
                throw new InvalidOperationException($"No way from ({d.X}, {d.Y}) to ({gx}, {gy}) in DNG{d.LevelId}.");
            }
            foreach (var dir in path)
            {
                var before = (d.X, d.Y);
                s.MoveDungeon(dir);
                if (s.Mode != GameMode.Dungeon || s.DungeonEventPrompt is not null || s.DungeonExitPending
                    || s.PendingTreasure.Count > 0 || s.PartyDefeatPending || (d.X, d.Y) == before)
                    break;
            }
            if (s.Mode == GameMode.Dungeon && (d.X, d.Y) == (gx, gy))
                return true;
            output.WriteLine($"attempt {attempt}: at ({d.X}, {d.Y}) cell {level.Get(d.X, d.Y):x2}");
        }
        return false;
    }

    private void Dump(GameSession s, int from)
    {
        foreach (var line in s.Log.Skip(from))
            output.WriteLine(line);
    }

    [OriginalDataFact]
    public void Stage1_NewGame_Guild_AndTownServices()
    {
        var s = StrongParty();
        Assert.Equal(6, s.Party.Members.Count);
        Assert.Equal("Pendragon", s.CurrentTown!.Name);

        // Armory: III's price is value · town index / 100 (Pendragon 150).
        var stock = s.CurrentTown.Stock.Select((n, id) => (id, n)).First(t => t.id > 0 && t.n > 0).id;
        var item = Phantasie3CampaignTests.Data.CreateItemCatalog().Get(stock)!;
        Assert.Equal(Phantasie3CampaignTests.Data.GetItem(stock).Value!.Value * 150 / 100, TownMenus.ListBuyPrice(item, s.CurrentTown));

        // Mystic: a new party is novice.
        Assert.StartsWith("You are novice adventurers", s.ConsultMystic().Message);

        // Training: short of XP, then trained with III's tables (level gold into the bank, D3-023).
        var fighter = s.Party.Members[0];
        Assert.True(s.CanTrain(fighter, out _));
        Assert.True(s.TrainingXpShortfall(fighter) > 0);
        Assert.Equal(TownTransactionKind.Failed, s.Train(fighter).Kind);
        fighter.Experience = 1_000_000;
        s.Party.GoldCarried = 10_000;
        var bank = fighter.GoldInBank;
        var cost = s.TrainingGoldCost(fighter);
        var trained = s.Train(fighter);
        Assert.Equal(TownTransactionKind.Ok, trained.Kind);
        Assert.Equal(2, fighter.Level);
        Assert.Equal(10_000 - cost, s.Party.GoldCarried);
        Assert.Equal(bank + fighter.P3.SocialClass * 256, fighter.GoldInBank);

        // The inn heals the body: one Broken leg (Down) is cured.
        var priest = s.Party.Members[1];
        var body = P3BodyAreas.Of(priest);
        body[P3BodyArea.LeftLeg] = P3AreaState.Broken;
        priest.BodyCondition = body.ToCondition();
        priest.Status = CharacterStatus.Down;
        s.RestAtInn();
        Assert.True(P3BodyAreas.Of(priest).AllOkay);
        Assert.Equal(CharacterStatus.Ok, priest.Status);
    }

    [OriginalDataFact]
    public void Stage2_Wilderness_ToTheArchives_Filmon_AndBackOut()
    {
        var s = StrongParty();
        var start = s.Log.Count;
        Assert.True(s.LeaveTown());
        Assert.True(WalkTo(s, 25, 39), "reached the archives' square");
        Assert.NotNull(s.EntryPrompt);
        Assert.Equal(OverworldEntryKind.Dungeon, s.EntryPrompt!.Kind);
        Assert.Contains("archives", s.EntryPrompt.Text, StringComparison.OrdinalIgnoreCase);
        s.AnswerOverworldEntry(true);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal(5, s.Dungeon!.LevelId);
        s.AnswerDungeonExit(takeIt: false, saveMap: false);

        // Filmon (16, 26): he reads the party and names the first task (rescue Kilmor in the Hall of Giants).
        Assert.True(WalkInDungeon(s, 15, 26), "beside Filmon");
        var mark = s.Log.Count;
        s.MoveDungeon(CardinalDirection.East);
        Settle(s);
        Dump(s, mark);
        Assert.True(WalkInDungeon(s, 2, 28), "beside the wand pedestal");
        var seq = s.LogSequence; // the log keeps 200 lines; III's fights on the way can write more
        s.MoveDungeon(CardinalDirection.West);
        Settle(s);
        Assert.Contains(s.LogSince(seq), l => l.Contains("The wand is cursed", StringComparison.Ordinal));
        Assert.All(s.Party.Members, m => Assert.Equal(4, m.P3.QuestBits & 4)); // quest bit 2: the Wand of Nikademus
        Assert.Contains(180, s.Sack.ItemIds);

        // Out by the entrance: back on the archives' own square.
        Assert.True(WalkInDungeon(s, 11, 1), "below the entrance");
        s.MoveDungeon(CardinalDirection.North);
        Assert.True(s.DungeonExitPending);
        s.AnswerDungeonExit(takeIt: true, saveMap: true);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((0, 25, 39), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(5, s.DungeonSave!.DungeonId); // the map was saved (the pedestal stays spent)
    }

    // ==== 3. Save and load in the archives ===========================================================================

    [OriginalDataFact]
    public void Stage3_SaveInTheArchives_LoadAndWalkOut()
    {
        var s = StrongParty();
        s.LeaveTown();
        Assert.True(WalkTo(s, 25, 39));
        s.AnswerOverworldEntry(true);
        s.AnswerDungeonExit(takeIt: false, saveMap: false);
        Assert.True(WalkInDungeon(s, 11, 3));
        s.Party.Members[0].P3.QuestBits = 0b101;
        s.Party.Members[1].P3.SavedAction = (int)P3Action.Parry;
        var days = s.TravelTime;
        Assert.True(days > 0); // thirds of a day: the walk out and the dungeon steps
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.IsType<Phantasie3Campaign>(t.Campaign);
        Assert.Equal(GameMode.Dungeon, t.Mode);
        Assert.Equal((5, 11, 3), (t.Dungeon!.LevelId, t.Dungeon.X, t.Dungeon.Y));
        Assert.Equal(4224, t.Dungeon.Level!.BufferLength);
        Assert.Equal(0b101, t.Party.Members[0].Phantasie3!.QuestBits);
        Assert.True(t.Party.Members[1].P3.ParrySaved); // D3-036 dos: the saved option +0x6e
        Assert.Equal(days, t.TravelTime);
        Assert.Equal(s.Party.Members[0].P3.SocialClass, t.Party.Members[0].P3.SocialClass);
        Assert.True(WalkInDungeon(t, 11, 1));
        t.MoveDungeon(CardinalDirection.North);
        t.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal((0, 25, 39), (t.Overworld.RegionId, t.Overworld.X, t.Overworld.Y));
    }

    [OriginalDataFact]
    public void Stage3b_FightSavedMidWay_LoadsWithIIIsCombat()
    {
        var s = StrongParty();
        s.LeaveTown();
        s.BeginCombat(0); // Skeletons
        Assert.NotNull(P3Fight.Of(s.Combat!));
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(GameMode.Combat, t.Mode);
        Assert.NotNull(P3Fight.Of(t.Combat!)); // the fight state is rebuilt, not lost
        var hp = t.Combat!.AllMonsters.Sum(m => m.HitPoints ?? 0);
        for (var round = 0; round < 5 && t.Mode == GameMode.Combat; round++)
        {
            for (var i = 0; i < t.Party.Members.Count; i++)
                t.ChooseAction(i, CombatChoice.Thrust);
            t.ResolveCombatRound();
        }
        Assert.True(t.Mode != GameMode.Combat || t.Combat!.AllMonsters.Sum(m => m.HitPoints ?? 0) < hp,
            "III's Thrust hurt the monsters after the load");
    }

    [OriginalDataFact]
    public void Stage3c_FightStateSaved_BodiesSpellsParriesRoundState()
    {
        var s = StrongParty();
        s.LeaveTown();
        s.BeginCombat(0);
        var fight = P3Fight.Of(s.Combat!)!;
        var monster = s.Combat!.AllMonsters.First();
        fight.BodyOf(monster)[P3BodyArea.LeftArm] = P3AreaState.Broken;
        var archer = s.Party.Members[0];
        var other = s.Party.Members[1];
        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameII, s.Combat.Rules);
        fight.SetBowSpell(archer, P3BowSpell.Accuracy, s.Combat.Rules);
        fight.SetBowSpell(other, P3BowSpell.Accuracy, s.Combat.Rules);
        fight.SetAttack(other, 33);
        fight.SetArmour(archer, 7);
        fight.SetParried(other);
        fight.WakeChance = 45;
        fight.FreeFlee = true;
        fight.NextRound = P3RoundMode.PartyOnly;
        fight.AwakenAtRoundEnd = true;
        fight.StartStrength = 1234;
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(json));
        var f = P3Fight.Of(t.Combat!)!;
        var m = t.Combat!.AllMonsters.First();
        Assert.Equal(P3AreaState.Broken, f.BodyOf(m)[P3BodyArea.LeftArm]);
        Assert.Equal(P3AreaState.Okay, f.BodyOf(m)[P3BodyArea.Head]);
        Assert.Equal(P3BowSpell.ArrowFlameII, f.BowSpellOf(t.Party.Members[0]));
        Assert.True(f.HasAccuracy(t.Party.Members[0]));
        Assert.True(f.HasAccuracy(t.Party.Members[1]));
        Assert.Equal(P3BowSpell.None, f.BowSpellOf(t.Party.Members[1]));
        Assert.Equal(33, f.AttackOf(t.Party.Members[1]));
        Assert.Equal(t.Party.Members[0].Skills.Attack, f.AttackOf(t.Party.Members[0]));
        Assert.Equal(7, f.ArmourOf(t.Party.Members[0]));
        Assert.True(f.Parried(t.Party.Members[1]));
        Assert.False(f.Parried(t.Party.Members[0]));
        Assert.True(f.UseParryBlock(t.Party.Members[1]));
        Assert.Equal((45, true, P3RoundMode.PartyOnly, true, 1234),
            (f.WakeChance, f.FreeFlee, f.NextRound, f.AwakenAtRoundEnd, f.StartStrength));
    }

    [OriginalDataFact]
    public void Stage3e_ElementalWithTwoCharacters_SummonedSavedAndDismissed()
    {
        var s = WithParty(new SeededDosRandomSource(11), members: 2);
        foreach (var m in s.Party.Members)
        {
            m.HpMax = m.HpCur = 5000;
            m.Skills = m.Skills with { Attack = 250 };
        }
        var wizard = s.Party.Members[1];
        wizard.SpellsKnown[SpellId.SummonElemental] = 1;
        wizard.MagicMax = wizard.MagicCur = 30;
        s.OpenGuild();
        Assert.True(s.TryAddElemental(0, out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0);
        s.ChooseAction(0, CombatChoice.Thrust);
        s.ChooseAction(1, CombatChoice.Cast, spellIndex: SpellId.SummonElemental);
        s.ResolveCombatRound();
        Assert.Equal(GameMode.Combat, s.Mode); // seed 11: the Skeletons are still there after round 1
        Assert.True(s.Party.ElementalSummoned); // III: no six-member test (root 0xc912)
        Assert.Contains("The elemental begins to form...", s.Log); // root 0xc912 (DS 0x656a), s97
        Assert.Equal(3, s.Party.Members.Count);
        var fight = P3Fight.Of(s.Combat!)!;
        fight.SetAttack(s.Party.Elemental!, 77);

        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.True(t.Party.ElementalSummoned);
        Assert.Equal(2, t.Party.Adventurers.Count());
        Assert.Equal(77, P3Fight.Of(t.Combat!)!.AttackOf(t.Party.Elemental!)); // member indices line up, elemental last
        t.FightToEnd();
        Assert.Equal(2, t.Party.Members.Count); // dismissed at the fight's end
        Assert.NotNull(t.Party.Elemental);
    }

    [OriginalDataFact]
    public void Stage3d_IIIQuestionsSaved_EntryNameTrapQuestionPaddedMenu()
    {
        // "<name> Will you enter?" survives a load.
        var s = StrongParty();
        s.LeaveTown();
        Assert.True(WalkTo(s, 25, 39));
        Assert.NotNull(s.EntryPrompt?.Name);
        var text = s.EntryPrompt!.Text;
        var t = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal(text, t.EntryPrompt!.Text);

        // A synthetic Yes / No question (III's trap / door prompt id −1) and a padded menu come back as they were.
        s.AnswerOverworldEntry(true);
        s.AnswerDungeonExit(takeIt: false, saveMap: false);
        foreach (var (id, lines, options) in new (int, string[], string[])[]
                 {
                     (-1, ["ARCHER spots a trap. Disarm it?"], [SessionPrompts.Yes, SessionPrompts.No]),
                     (3, ["A message."], ["Leave", "2", "3"]),
                 })
        {
            s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(4, id, new DungeonMessage(lines, options));
            var u = GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(12));
            u.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
            Assert.Equal(id, u.DungeonEventPrompt!.MessageId);
            Assert.Equal(lines, u.DungeonEventPrompt.Message.Lines);
            Assert.Equal(options, u.DungeonEventPrompt.Message.Options);
        }
    }

    // ==== 4. The planes: the crystal building, Light, Dark, the Netherworld ==========================================

    /// <summary>Enter dungeon <paramref name="n"/> from where the party stands (its entrance is an exit cell).</summary>
    private static void EnterHere(GameSession s, int n)
    {
        Assert.NotNull(s.EntryPrompt);
        Assert.Equal((OverworldEntryKind.Dungeon, n), (s.EntryPrompt!.Kind, s.EntryPrompt.Id));
        s.AnswerOverworldEntry(true);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal(n, s.Dungeon!.LevelId);
    }

    /// <summary>Leave by the exit cell under the party after the lever / break-the-wand set it (dungeon-events.md §5).</summary>
    private static void TakeExitAs(GameSession s, byte exitCell)
    {
        var d = s.Dungeon!;
        d.Level!.Set(d.X, d.Y, exitCell);
        s.CampaignAccess.DungeonExitPending = true;
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal(GameMode.Overworld, s.Mode);
    }

    [OriginalDataFact]
    public void Stage4_CrystalBuilding_ToTheLightAndDarkPlanes_AndTheNetherworld()
    {
        var s = StrongParty();
        s.LeaveTown();
        Assert.True(WalkTo(s, 48, 36), "reached the crystal building");
        EnterHere(s, 3);
        TakeExitAs(s, 0xd8); // "C2+" sets the lever's square to 0xd8: destination 29
        Assert.Equal((1, 4, 1), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));

        // The Light plane: the town of Light on the arrival square, the Castle of Light at (6, 7).
        Assert.True(WalkTo(s, 6, 7), "reached the Castle of Light");
        EnterHere(s, 10);
        s.AnswerDungeonExit(takeIt: true, saveMap: false); // destination 29: stays on Light, on the castle's square
        Assert.Equal((1, 6, 7), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.True(WalkTo(s, 4, 1), "back at the town of Light");
        Assert.Equal(OverworldEntryKind.Town, s.EntryPrompt!.Kind);
        Assert.Equal(15, s.EntryPrompt.Id);
        s.AnswerOverworldEntry(true);
        Assert.Equal("Light", s.CurrentTown!.Name);
        Assert.True(s.LeaveTown());
        Assert.Equal((1, 4, 1), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));

        // The Dark plane through the crystal building's other lever setting, then the Castle of Dark.
        s.Overworld.RegionId = 0;
        s.Overworld.X = 48;
        s.Overworld.Y = 35;
        s.MoveOverworld(CardinalDirection.South);
        EnterHere(s, 3);
        TakeExitAs(s, 0xd9); // "C2−": destination 31 (its x, y bytes are ignored: live s82)
        Assert.Equal((2, 4, 1), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.True(WalkTo(s, 2, 7), "reached the Castle of Dark");
        EnterHere(s, 11);
        var d = s.Dungeon!;
        d.X = 3; // "break the wand" teleports the party to (3, 1); the exit 0xd8 at (3, 0) leads to the Netherworld
        d.Y = 0;
        s.CampaignAccess.DungeonExitPending = true;
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal((4, 7, 3), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(31, s.CurrentOverworldRegion.GridWidth);
        Assert.Equal(16, s.CurrentOverworldRegion.GridHeight);

        // The Netherworld: the dark fortress at (6, 10).
        Assert.True(WalkTo(s, 6, 10), "reached the dark fortress");
        EnterHere(s, 9);
    }

    // ==== 5. The dark fortress: Nikademus, Lord Wood, the gods' reward ==============================================

    private GameSession AtTheFortress(int seed = 11)
    {
        var s = StrongParty(seed);
        s.LeaveTown();
        s.Overworld.RegionId = Phantasie3Campaign.Netherworld;
        s.Overworld.X = 5; // the Black Path west of the fortress (the walk there is Stage 4's)
        s.Overworld.Y = 10;
        s.MoveOverworld(CardinalDirection.East);
        EnterHere(s, 9);
        s.AnswerDungeonExit(takeIt: false, saveMap: false);
        s.Dungeon!.WanderingChance = 0; // no wandering fights on the way (the fortress's level-5 monsters are not the point)
        Assert.True(WalkInDungeon(s, 10, 16), "below Nikademus's square");
        return s;
    }

    /// <summary>The fight in progress, its monsters at 1 HP, fought to the end.</summary>
    private static CombatResult WinRigged(GameSession s)
    {
        Assert.Equal(GameMode.Combat, s.Mode);
        foreach (var m in s.Combat!.AllMonsters)
            m.HitPoints = 1;
        return s.FightToEnd();
    }

    [OriginalDataFact]
    public void Stage5_Nikademus_ZeusMakesThePartySuperheroes()
    {
        var s = AtTheFortress();
        var mark = s.Log.Count;
        s.MoveDungeon(CardinalDirection.North); // (10, 15): Nikademus appears (no member has score 200)
        Settle_ToMenu(s);
        var menu = Assert.IsType<PendingDungeonEvent>(s.DungeonEventPrompt);
        Assert.Equal(3, menu.Message.Options.Count);
        s.AnswerDungeonEvent(1); // deeds < 8: he fights at once (monster 77)
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(Phantasie3Campaign.NikademusA, s.Combat!.FirstMonsterId);
        WinRigged(s);
        Dump(s, mark);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Equal((0, 24, 37), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.All(s.Party.Members, m =>
        {
            Assert.Equal(Phantasie3CharacterData.Superhero, m.P3.SocialClass);
            Assert.Equal(250, m.Score);
            Assert.Equal(CharacterStatus.Ok, m.Status);
        });
        Assert.Contains(s.Log, l => l.Contains("Zeus.", StringComparison.Ordinal));
        // The scene (Amiga 0x1ee82): w1 (Zeus) with the party under it, then the speech; Continue closes it, a load drops it.
        var scene = Assert.IsType<RewardScene>(s.PendingRewardScene);
        Assert.True(scene.Good);
        Assert.Equal(s.Party.Members.Where(m => !ReferenceEquals(m, s.Party.Elemental)).Select(m => m.Race), scene.PartyRaces);
        Assert.Equal(2, scene.IntroLines.Count);
        Assert.Contains("Zeus.", scene.SpeechLines[0], StringComparison.Ordinal);
        var prompts = new SessionPrompts(s);
        while (prompts.Current!.Kind == SessionPromptKind.Treasure) // the fight's own finds are asked first (ovl4 0x1d30)
            prompts.Answer(yes: false);
        Assert.Equal(SessionPromptKind.RewardScene, prompts.Current!.Kind);
        Assert.Equal(scene.Lines, prompts.Current.Lines);
        prompts.Answer(1);
        Assert.Null(s.PendingRewardScene);
        // D3-071: the arrival in Pendragon pays the fight's XP out (DOS 0xa911 keeps the pool on the endgame's arrival).
        while (prompts.Current is { Kind: SessionPromptKind.ExperienceShares })
            prompts.Answer(1);
        Assert.Null(prompts.Current);
        Assert.Equal(0, s.Sack.Experience);
        Assert.EndsWith("and heroes of the Scandor.", s.ConsultMystic().Message);
    }

    [OriginalDataFact]
    public void Stage5b_LordWood_PlutoMakesThePartyDarklords()
    {
        var s = AtTheFortress(seed: 23);
        foreach (var m in s.Party.Members)
            m.P3.DeedBits = 8; // evil enough for Nikademus to summon Lord Wood (dungeon-events.md §5)
        var mark = s.Log.Count;
        s.MoveDungeon(CardinalDirection.North);
        Settle_ToMenu(s);
        s.AnswerDungeonEvent(1);
        Settle_ToMenu(s);
        Dump(s, mark);
        var choice = Assert.IsType<PendingDungeonEvent>(s.DungeonEventPrompt);
        Assert.Equal("1)Fight Lord Wood", choice.Message.Options[0]);
        s.AnswerDungeonEvent(1);
        Assert.Equal(Phantasie3Campaign.LordWood, s.Combat!.FirstMonsterId);
        WinRigged(s);
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie3CharacterData.Darklord, m.P3.SocialClass));
        Assert.All(s.Party.Members, m => Assert.Equal(200, m.Score));
        Assert.Contains(s.Log, l => l.Contains("Pluto.", StringComparison.Ordinal));
        Assert.False(s.PendingRewardScene!.Good); // w0 (Pluto)
        Assert.Contains("Darklords.", s.PendingRewardScene.SpeechLines[1], StringComparison.Ordinal);
        s.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Null(s.PendingRewardScene); // only the scene is dropped by a load; the titles are saved
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie3CharacterData.Darklord, m.P3.SocialClass));
        Assert.EndsWith("and heroes of the Netherworld.", s.ConsultMystic().Message);
    }

    /// <summary>Acknowledge plain one-choice texts until a menu with choices waits.</summary>
    private static void Settle_ToMenu(GameSession s)
    {
        for (var i = 0; i < 10 && (s.DungeonEventPrompt is { Message.Options.Count: < 2 } || s.DungeonPage is { WaitsForKey: true }); i++)
        {
            if (s.DungeonPage is { WaitsForKey: true })
                s.AcknowledgeDungeonPage();
            else
                s.AnswerDungeonEvent(1);
        }
    }

    // ==== 6. A party defeat: the Astral Plane ========================================================================

    [OriginalDataFact]
    public void Stage6_PartyDefeat_TheAstralPlane_SendsSurvivorsToARandomTown()
    {
        var s = StrongParty();
        s.LeaveTown();
        foreach (var m in s.Party.Members)
        {
            m.Level = 10; // level > (r & 3) + 2 always: resurrected
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        s.Sack.ItemIds.Add(101);
        s.Sack.Experience = 5000;
        s.Party.GoldCarried = 777;
        var con = s.Party.Members.Select(m => m.Stats.Con).ToArray();
        s.CampaignAccess.PartyDefeatPending = true;
        var result = s.ResolvePartyDefeat();
        Assert.All(result.Fates, f => Assert.Equal(DefeatFate.Resurrected, f.Fate));
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.InRange(s.CurrentTownId!.Value, 1, 14);
        Assert.Equal(0, s.Overworld.RegionId);
        Assert.Empty(s.Sack.ItemIds);
        Assert.Equal(0, s.Sack.Experience);
        Assert.Equal(777, s.Party.GoldCarried);
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(con[i] - 1, s.Party.Members[i].Stats.Con);
            Assert.Equal(CharacterStatus.Ok, s.Party.Members[i].Status);
            Assert.True(s.Party.Members[i].HpCur >= 1);
        }
        Assert.Contains(s.Log, l => l.Contains("Astral Plane", StringComparison.Ordinal));
    }
}
