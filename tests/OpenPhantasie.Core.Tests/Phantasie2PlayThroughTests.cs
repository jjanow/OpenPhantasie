using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T3 ("a full II play-through", PLAN.md T3 leftovers): a scripted, seeded run of Phantasie II on the shipped
/// <c>GameData/phantasie2</c> through the session API, from a new game to the Filmon ending. Each stage is its own test,
/// so one gap does not hide the later ones: 1 new game, Guild, town services; 2 wilderness → DNG1 → fight → exit → loot,
/// training, the wilderness inn; 3 TRANSPORTATION, the Netherworld Guild, every town's exit; 4 the quest chain (minions,
/// amulets, runes, the DNG5 orb, the Mystic, the DNG8 dragon, Filmon, the wand); 5 party defeat; 6 save / load in a
/// dungeon; 7 sweeps of every dungeon and overworld cell; 8 on foot from DNG5's / DNG8's entrance to the orb / the dragon;
/// 9 the menu data invariant behind DNG8's door. Fights are won with test power (as the other II tests); a few
/// stages start from a prepared jump, named where they do.
/// The point is to catch campaign gaps: a throw, a soft-lock, a dead end, or I's data / rules where II's apply.
/// Routes are read off the shipped maps: PIPPACOTT's exit OUT2 (16,3) → N → W×6 = DNG1's entrance OUT2 (10,2) (its
/// dest (15,0) is DNG1's exit cell 0xd7, whose record 0 leads back to OUT2 (10,2)); in DNG1, S,S,W,W,S,S from (15,0)
/// reaches the forced fight cell 0x80 at (13,4) over wandering-fight cells 0x0f.
/// </summary>
public class Phantasie2PlayThroughTests
{
    private static readonly GameDataRepository Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private const CardinalDirection N = CardinalDirection.North, S = CardinalDirection.South,
        E = CardinalDirection.East, W = CardinalDirection.West;

    /// <summary>PIPPACOTT's exit tile (towns.json) to DNG1's entrance OUT2 (10,2).</summary>
    private static readonly CardinalDirection[] PippacottToDng1 = [N, W, W, W, W, W, W];

    /// <summary>DNG1 (15,0) → the forced fight 0x80 at (13,4).</summary>
    private static readonly CardinalDirection[] Dng1EntryToFight = [S, S, W, W, S, S];

    // ==== Helpers ===================================================================================================

    private static GameSession NewGame(int seed = 2026, GameOptions? options = null)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        s.StartNewGame(options);
        return s;
    }

    private static readonly (string Name, Race Race, CharacterClass Class)[] Roster =
    [
        ("GLADE", Race.Human, CharacterClass.Fighter), ("YOSHI", Race.Dwarf, CharacterClass.Fighter),
        ("ABBOT", Race.Elf, CharacterClass.Priest), ("MERLA", Race.Elf, CharacterClass.Wizard),
        ("SLINK", Race.Halfling, CharacterClass.Thief), ("RANGO", Race.Gnome, CharacterClass.Ranger),
    ];

    /// <summary>The Guild in the current town: N)EW six members and A)DD them.</summary>
    private static void FormParty(GameSession s, int members = 6)
    {
        s.OpenGuild();
        Assert.Equal(GameMode.Guild, s.Mode);
        foreach (var (name, race, cls) in Roster.Take(members))
        {
            var rolled = s.RollNewMember(race, cls);
            Assert.True(s.TryEnrollNewMember(rolled, name, out var err), err);
            Assert.True(s.TryAddToParty(rolled, out err), err);
        }
        s.LeaveGuild();
        Assert.Equal(GameMode.Town, s.Mode);
    }

    /// <summary>Test power (as the other II tests): enough HP and hitting power that seeded fights end in a win.</summary>
    private static void Empower(GameSession s)
    {
        foreach (var m in s.Party.Members)
        {
            m.HpMax = m.HpCur = 600;
            m.Skills = m.Skills with { Attack = Math.Max(m.Skills.Attack, 60) };
            m.WeaponDamageA = Math.Max(m.WeaponDamageA, 30);
        }
    }

    /// <summary>Answers whatever the session waits on (fights, treasure, menus, prompts) until it waits on nothing.</summary>
    private sealed class Driver(GameSession s)
    {
        public int Fights;
        public int Wins;
        public readonly List<int> FirstMonsters = new();

        /// <summary>
        /// Exploring: disarm spotted traps, face lock ambushes instead of stepping back, and answer event menus with the
        /// last option first, then the others in turn on later visits (<see cref="NextChoice"/>).
        /// </summary>
        public bool Bold;

        private readonly Dictionary<(byte, int), int> _menuSeen = new();

        public int NextChoice(PendingDungeonEvent? e)
        {
            var key = (e!.Feature, e.MessageId);
            var seen = _menuSeen.GetValueOrDefault(key);
            _menuSeen[key] = seen + 1;
            var n = e.Message.Options.Count;
            return n - seen % n;
        }

        public void Settle(bool enterOnEntryPrompt = false)
        {
            for (var guard = 0; guard < 100; guard++)
            {
                if (s.PartyDefeatPending)
                {
                    s.ResolvePartyDefeat();
                    continue;
                }
                if (s.Mode == GameMode.Combat)
                {
                    Fights++;
                    FirstMonsters.Add(s.Combat!.FirstMonsterId);
                    if (Bold) // the explorer tests the map, not the fights
                        foreach (var mon in s.Combat.AllMonsters)
                            (mon.Defense, mon.Armor) = (0, 0);
                    var r = s.FightToEnd();
                    Assert.True(r.EncounterOver, "a fight did not end within FightToEnd's rounds");
                    if (r.PartyWon)
                        Wins++;
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
                    s.AnswerOverworldEntry(enterOnEntryPrompt);
                    continue;
                }
                if (s.DungeonEventPrompt is not null)
                {
                    s.AnswerDungeonEvent(Bold ? NextChoice(s.DungeonEventPrompt) : 1);
                    continue;
                }
                if (s.DungeonTrapPrompt is not null)
                {
                    s.AnswerDungeonTrap(Bold && s.DungeonTrapPrompt!.Disarmers.Count > 0 ? s.DungeonTrapPrompt.Disarmers[0] : null);
                    continue;
                }
                if (s.DungeonLockPrompt is not null)
                {
                    s.AnswerDungeonLock(!Bold); // bold: face the ambush and walk on
                    continue;
                }
                return;
            }
            Assert.Fail("the session kept asking for 100 answers in a row (soft-lock?)");
        }

        /// <summary>Walk the overworld, settling after each step; the last step's entry prompt is left open.</summary>
        public void Walk(IEnumerable<CardinalDirection> dirs)
        {
            var list = dirs.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                Assert.Equal(GameMode.Overworld, s.Mode);
                s.MoveOverworld(list[i]);
                if (i < list.Count - 1)
                    Settle();
                else if (s.EntryPrompt is null)
                    Settle();
            }
        }

        /// <summary>Step in the dungeon, settling after each step (fights resolved, prompts answered).</summary>
        public void Step(IEnumerable<CardinalDirection> dirs)
        {
            foreach (var d in dirs)
            {
                Assert.Equal(GameMode.Dungeon, s.Mode);
                s.MoveDungeon(d);
                Settle();
                Assert.False(s.DungeonExitPending && d != dirs.Last(), "an unexpected dungeon exit");
            }
        }
    }

    private static CardinalDirection Back(CardinalDirection d) => d switch
    {
        N => S, S => N, E => W, _ => E,
    };

    // ==== 1. New game, Guild, town services ==========================================================================

    [OriginalDataFact]
    public void Stage1_NewGame_Guild_TownServices_InPippacott()
    {
        var s = NewGame();
        Assert.Same(Phantasie2Campaign.Instance, s.Campaign);
        Assert.Equal("PIPPACOTT", s.CurrentTown!.Name);
        Assert.Empty(s.Party.Members);
        Assert.False(s.LeaveTown()); // an empty party may not leave

        FormParty(s);
        Assert.Equal(6, s.Party.Members.Count);
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie2Origin.Ferronrah, m.Phantasie2!.Origin));
        Assert.All(s.Party.Members, m => Assert.Equal(s.Rules.StartingGold, m.GoldInBank));
        Assert.Equal(Data.Rules.PartyGoldCap, s.Sack.GoldCap);

        // Bank: withdraw everything, then put some back by shares.
        foreach (var m in s.Party.Members)
            Assert.Equal(TownTransactionKind.Ok, s.BankWithdraw(m, m.GoldInBank).Kind);
        Assert.Equal(6 * s.Rules.StartingGold, s.Party.GoldCarried);

        // Armory: PIPPACOTT stocks 5 DAGGERs (towns.json; no restock on a new game).
        var fighter = s.Party.Members[0];
        Assert.Equal("DAGGER", Data.Items.First(i => i.Id == 46).Name);
        var purse = s.Party.GoldCarried;
        var buy = s.Buy(fighter, 46);
        Assert.Equal(TownTransactionKind.Ok, buy.Kind);
        Assert.Contains(46, fighter.InventoryItemIds);
        Assert.True(s.Party.GoldCarried < purse);

        Assert.Equal(TownTransactionKind.Ok, s.BankDeposit([1, 1, 1, 1, 1, 1]).Kind);

        // Inn, Mystic (II's: a quest SCORE, not I's level score).
        Assert.Equal(TownTransactionKind.Ok, s.RestAtInn().Kind);
        var mystic = s.ConsultMystic();
        Assert.StartsWith("Your score is", mystic.Message);
        Assert.All(s.Party.Members, m => Assert.Equal(0, m.P2.QuestScore));

        Assert.True(s.LeaveTown());
        Assert.Equal((2, 16, 3), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    /// <summary>A new game with an empowered party (six by default) standing on PIPPACOTT's exit.</summary>
    private static (GameSession S, Driver D) Outside(int seed = 2026, GameOptions? options = null, int members = 6)
    {
        var s = NewGame(seed, options);
        FormParty(s, members);
        Empower(s);
        Assert.True(s.LeaveTown());
        return (s, new Driver(s));
    }

    // ==== 2. Wilderness → DNG1 → fight → exit → back to town ==========================================================

    [OriginalDataTheory]
    [InlineData(2026)]
    [InlineData(7)]
    [InlineData(81)]
    public void Stage2_WalkToDng1_Fight_Leave_AndCashInAtPippacott(int seed)
    {
        var (s, d) = Outside(seed);
        d.Walk(PippacottToDng1);
        Assert.Equal((2, 10, 2), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(OverworldEntryKind.Dungeon, s.EntryPrompt!.Kind);
        Assert.Equal(1, s.EntryPrompt.Id);
        s.AnswerOverworldEntry(true);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal((1, 15, 0), (s.Dungeon!.LevelId, s.Dungeon.X, s.Dungeon.Y));

        // Down to the forced fight at (13,4): its monster is II's (monsters.json), and the party wins.
        d.Step(Dng1EntryToFight.SkipLast(1));
        var fightsBefore = d.Fights;
        var move = s.MoveDungeon(Dng1EntryToFight[^1]);
        Assert.Equal(DungeonMoveKind.TriggerCombat, move.Kind);
        Assert.Equal(GameMode.Combat, s.Mode);
        var first = s.Combat!.FirstMonsterId;
        Assert.Equal(Data.GetMonster(first).Name, s.Combat.Ranks.First(r => r.RankIndex == 0).Members[0].Name);
        d.Settle();
        Assert.Equal(fightsBefore + 1, d.Fights);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal((13, 4), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.True(s.Sack.Experience > 0 || s.Sack.Gold > 0, "a won fight puts its XP / gold in the sack");

        // Back the way we came, to the exit cell: "AN EXIT. TAKE IT?" lands on DNG1's OUT tile.
        var back = Dng1EntryToFight.Reverse().Select(Back).ToArray();
        d.Step(back);
        Assert.Equal((15, 0), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.True(s.DungeonExitPending);
        s.AnswerDungeonExit(true, saveMap: false);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((2, 10, 2), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));

        // Home: E×6, S onto PIPPACOTT's tile, enter, split the sack.
        d.Walk(PippacottToDng1.Reverse().Select(Back));
        Assert.Equal(OverworldEntryKind.Town, s.EntryPrompt!.Kind);
        Assert.Equal(1, s.EntryPrompt.Id);
        s.AnswerOverworldEntry(true);
        Assert.Equal("PIPPACOTT", s.CurrentTown!.Name);
        var xp = s.Sack.Experience;
        var before = s.Party.Members.Sum(m => m.Experience);
        Assert.Equal(TownTransactionKind.Ok, s.DistributeLoot().Kind);
        Assert.True(s.Sack.IsEmpty);
        Assert.True(s.Party.Members.Sum(m => m.Experience) >= before + xp - s.Party.Members.Count);

        // Training: with XP to spare, the Guild trains a fighter by II's table (rules.json "training").
        var glade = s.Party.Members[0];
        glade.Experience += 100_000;
        glade.GoldInBank = 60_000;
        s.Party.GoldCarried = 60_000;
        Assert.True(s.CanTrain(glade, out var why), why);
        var level = glade.Level;
        Assert.Equal(TownTransactionKind.Ok, s.Train(glade).Kind);
        Assert.Equal(level + 1, glade.Level);
        Assert.True(s.LeaveTown());
    }

    /// <summary>The wilderness inn OUT2 (12,0), seven steps from PIPPACOTT: the stay works on II's data.</summary>
    [OriginalDataFact]
    public void Stage2b_WildernessInn_NearPippacott()
    {
        var (s, d) = Outside();
        foreach (var m in s.Party.Members)
            m.HpCur = 10;
        d.Walk([N, N, W, W, W, W, N]);
        Assert.Equal((2, 12, 0), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        s.Party.GoldCarried = 5000;
        var stay = s.StayAtWildernessInn();
        Assert.Equal(TownTransactionKind.Ok, stay.Kind);
        Assert.All(s.Party.Members, m => Assert.Equal(m.HpMax, m.HpCur));
    }

    /// <summary>D2-042: the stay ages everyone two weeks (manual) or adds the ST's 1000 travel-time units and ages nobody.</summary>
    [OriginalDataTheory]
    [InlineData("manual")]
    [InlineData("st")]
    public void D2042_WildernessInnStay_TwoWeeks_OrStTravelTime(string value)
    {
        var (s, d) = Outside(options: GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.P2WildernessInnTime.Id, value));
        d.Walk([N, N, W, W, W, W, N]);
        s.Party.GoldCarried = 5000;
        var age = s.Party.Members[0].AgeClock;
        var time = s.TravelTime;
        Assert.Equal(TownTransactionKind.Ok, s.StayAtWildernessInn().Kind);
        var st = value == "st";
        Assert.Equal(st ? age : age + TownMenus.InnStayAgeClockUnits, s.Party.Members[0].AgeClock);
        Assert.Equal(st ? time + 1000 : time, s.TravelTime);
    }

    // ==== 3. TRANSPORTATION between towns, the Netherworld Guild, every town's exit ==========================================

    /// <summary>The Wizard learns TRANSPORTATION (index 53, as I) with MP to spare.</summary>
    private static Character Transporter(GameSession s)
    {
        var w = s.Party.Members.First(m => m.Class == CharacterClass.Wizard);
        var spells = w.SpellsKnown;
        if (spells.Length <= SpellId.Transportation)
            Array.Resize(ref spells, SpellId.Transportation + 1);
        spells[SpellId.Transportation] = 1;
        w.SpellsKnown = spells;
        w.MagicMax = w.MagicCur = 50;
        return w;
    }

    private static void Transport(GameSession s, Character caster, int townId, string name)
    {
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Contains(townId, s.TransportDestinations);
        var cast = s.CastSpell(caster, SpellId.Transportation);
        Assert.True(cast.Success, cast.Message);
        Assert.True(s.TransportationPending);
        Assert.True(s.ChooseTransportDestination(townId));
        Assert.False(s.TransportationPending);
        Assert.Equal(townId, s.CurrentTownId);
        Assert.Equal(name, s.CurrentTown!.Name);
    }

    [OriginalDataFact]
    public void Stage3_Transportation_ToTheNetherworld_GuildThere_AndBack()
    {
        var s = NewGame();
        FormParty(s, members: 5);
        Empower(s);
        var wizard = Transporter(s);
        // D2-030 (manual): every other town 1..10, Ferronrah and the Netherworld.
        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 9, 10], s.TransportDestinations);

        Transport(s, wizard, 7, "BLACK MOOR");
        // Town services work in a Netherworld town too.
        Assert.Equal(TownTransactionKind.Ok, s.RestAtInn().Kind);
        Assert.StartsWith("Your score is", s.ConsultMystic().Message);

        // The Guild here: a new member is from the Netherworld (ORIGIN by the Guild's plane) and joins the party.
        s.OpenGuild();
        var newcomer = s.RollNewMember(Race.Human, CharacterClass.Monk);
        Assert.True(s.TryEnrollNewMember(newcomer, "NETHA", out var err), err);
        Assert.Equal(Phantasie2Origin.Netherworld, newcomer.Phantasie2!.Origin);
        Assert.Equal(7, newcomer.CurrentTown);
        Assert.True(s.TryAddToParty(newcomer, out err), err);
        s.LeaveGuild();
        Assert.Equal(6, s.Party.Members.Count);
        Assert.All(s.Party.Members.Take(5), m => Assert.Equal(Phantasie2Origin.Ferronrah, m.Phantasie2!.Origin));

        // BLACK MOOR's exit is the one-way link to OUT6 (16,8) on Ferronrah (towns.md §3); the party can walk on.
        Assert.True(s.LeaveTown());
        Assert.Equal((6, 16, 8), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        var d = new Driver(s);
        foreach (var dir in new[] { N, S, E, W })
        {
            var step = s.MoveOverworld(dir);
            d.Settle();
            if (step.Kind != OverworldMoveKind.Blocked)
                break;
        }
        Assert.Equal(GameMode.Overworld, s.Mode);

        // Back into BLACK MOOR, on to DEATHPORT, then home to PIPPACOTT, all by TRANSPORTATION.
        s.EnterTown(7);
        Transport(s, wizard, 8, "DEATHPORT");
        Transport(s, wizard, 1, "PIPPACOTT");
        Assert.True(s.LeaveTown());
        Assert.Equal((2, 16, 3), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataFact]
    public void Stage3_StOption_TransportsOnlyOnTheSamePlane()
    {
        var s = NewGame(options: GameOptions.ForPreset(GameId.P2).With("p2.transport-list", "st"));
        FormParty(s, members: 4);
        var wizard = Transporter(s);
        Assert.Equal([2, 3, 4, 5, 6], s.TransportDestinations);
        Transport(s, wizard, 6, "FERRON");
        Assert.DoesNotContain(7, s.TransportDestinations);
        Assert.False(s.ChooseTransportDestination(7)); // nothing pending, and not on the list
    }

    /// <summary>
    /// Every reachable town: entered, left by its exit record (towns.json; BLACK MOOR and DEMONIAC are one-way links,
    /// towns.md §3; the other eight exits are their own tile-40 cell), and the party can take a step on the map there.
    /// </summary>
    [OriginalDataFact]
    public void Stage3_EveryTown_EntersLeavesAndWalksOn()
    {
        var (s, d) = Outside();
        var traversal = Data.CreateTraversal(Phantasie2Campaign.Instance);
        for (var town = 1; town <= Phantasie2Campaign.ReachableTownCount; town++)
        {
            s.EnterTown(town);
            Assert.Equal(GameMode.Town, s.Mode);
            var exit = s.CurrentTown!.ExitPosition!.Value;
            Assert.True(s.LeaveTown(), $"town {town}");
            Assert.Equal(exit, (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
            if (town is not 7 and not 9)
            {
                var r = traversal.GetRegion(exit.Region);
                Assert.Equal(40, r.GetNormalized(exit.X, exit.Y));
                Assert.Equal(town, r.FindFeature(exit.X, exit.Y)!.Arg);
            }
            var moved = false;
            foreach (var dir in new[] { N, E, S, W })
            {
                var step = s.MoveOverworld(dir);
                d.Settle();
                if (step.Kind != OverworldMoveKind.Blocked)
                {
                    moved = true;
                    break;
                }
            }
            Assert.True(moved, $"stuck outside town {town}");
            Assert.Equal(GameMode.Overworld, s.Mode);
        }
    }

    // ==== 4. The quest: minions → amulets → runes, the orb, the Mystic, the Ice Dragon, Filmon, the wand ===============

    /// <summary>The eight minions (0-based monster ids; reaction modifier −11..−18 = minion 1..8, monsters.json).</summary>
    private static readonly int[] MinionMonsters = [17, 43, 72, 73, 74, 75, 76, 77];

    /// <summary>Fight minion <paramref name="monsterId"/> in the wilderness with one-HP monsters; returns its number k.</summary>
    private static int KillMinion(GameSession s, Driver d, int monsterId)
    {
        var enc = s.BeginWildernessCombat(monsterId);
        var k = Phantasie2Campaign.MinionOf(enc);
        Assert.True(k is not null, $"monster {monsterId} is no minion");
        foreach (var mon in enc.AllMonsters)
        {
            mon.HitPoints = 1;
            mon.Defense = 0; // PLUTO'S WINGED DEVIL out-defends a level-1 party for 200 rounds
            mon.Armor = 0;
        }
        d.Settle();
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.True(s.Party.Members.All(m => m.P2.MinionKilled[k!.Value - 1]),
            $"minion {k} not killed:\n" + string.Join("\n", s.Log.TakeLast(20)));
        return k!.Value;
    }

    [OriginalDataFact]
    public void Stage4_Quest_MinionsRunesOrb_Mystic_DragonEnding_Wand()
    {
        var (s, d) = Outside();

        // Pluto's eight minions: each kill drops its amulet into the sack and flags every member.
        var killed = MinionMonsters.Select(id => KillMinion(s, d, id)).ToList();
        Assert.Equal(Enumerable.Range(1, 8), killed.Order());
        for (var k = 1; k <= 8; k++)
            Assert.Contains(Phantasie2Campaign.AmuletBase + k, s.Sack.Items);

        // Home to PIPPACOTT (on foot: PIPPACOTT is the exit tile, one step away and back).
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (2, 16, 2);
        s.MoveOverworld(S);
        Assert.Equal(OverworldEntryKind.Town, s.EntryPrompt!.Kind);
        s.AnswerOverworldEntry(true);
        Assert.Equal(1, s.CurrentTownId);

        // D)ISTRIBUTE hands the amulets out; EXAMINE on each gives every member the beast rune.
        Assert.Equal(TownTransactionKind.Ok, s.DistributeLoot().Kind);
        Assert.DoesNotContain(s.Sack.Items, i => i is > Phantasie2Campaign.AmuletBase and <= Phantasie2Campaign.AmuletBase + 8);
        for (var k = 1; k <= 8; k++)
        {
            var amulet = Phantasie2Campaign.AmuletBase + k;
            var holder = s.Party.Members.Single(m => m.InventoryItemIds.Contains(amulet));
            Assert.Equal(["EXAMINE", "WEAR", "LEAVE"], s.ItemUseOptions(holder, amulet)!);
            Assert.Equal("YOU ARE CHANGED", s.UseCampaignItem(holder, amulet, 0).Message);
            Assert.Contains(amulet, holder.InventoryItemIds); // kept
        }
        Assert.All(s.Party.Members, m => Assert.All(m.P2.BeastRunes, Assert.True));

        // Runes alone: s = 80 → rank 4 ("s > 50 without the orb"); the SCORE stays below the dragon's 100.
        s.ConsultMystic();
        Assert.All(s.Party.Members, m => Assert.Equal(80, m.P2.QuestScore));

        // The orb: DNG5 event 9, the pedestal at (16,33) (endgame.md §4), stepped onto from (16,32) (a prepared jump
        // into DNG5; its entrance is OUT17 (20,5)). "YOU FIND ---ORB---. KEEP IT?" → the sack → D)ISTRIBUTE.
        Assert.True(s.LeaveTown());
        s.EnterDungeon(5, 16, 32);
        var pedestal = s.MoveDungeon(S);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, pedestal.Kind);
        d.Settle();
        Assert.True(s.Sack.Items.Contains(Phantasie2Campaign.OrbId),
            "no orb from the DNG5 pedestal:\n" + string.Join("\n", s.Log.TakeLast(15)));
        Assert.Equal(GameMode.Dungeon, s.Mode);
        s.ExitDungeonToWilderness();
        s.EnterTown(1);
        Assert.Equal(TownTransactionKind.Ok, s.DistributeLoot().Kind);
        var bearer = s.Party.Members.Single(m => m.InventoryItemIds.Contains(Phantasie2Campaign.OrbId));
        var mystic = s.ConsultMystic(); // 20 + 80 = 100
        Assert.Equal("Your score is  100. You are ready to destroy the orb.", mystic.Message);
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie2Campaign.DragonGateScore, m.P2.QuestScore));

        // DNG8, one cell north of the Ice Dragon (prepared jump: DNG8 lies in the Netherworld, OUT30 (15,5)).
        Assert.True(s.LeaveTown());
        s.EnterDungeon(8, 32, 32);
        var move = s.MoveDungeon(S);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, move.Kind);
        s.AnswerDungeonEvent(2); // APPROACH (SCORE 100 passes)
        Assert.Equal(["ATTACK", "TALK", "FEED IT"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
        s.AnswerDungeonEvent(3); // FEED IT
        s.AnswerDungeonEvent(2); // THE ORB → SCORE 200, the ending

        Assert.Equal(EndgameOutcome.Victory, s.LastEndgame);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal("PIPPACOTT", s.CurrentTown!.Name);
        Assert.Null(s.Dungeon);
        Assert.All(s.Party.Members, m => Assert.Equal(Phantasie2Campaign.OrbDestroyedScore, m.P2.QuestScore));
        Assert.Equal(6, s.Sack.Items.Count(i => i == Phantasie2Campaign.FilmonWandId));
        Assert.Contains(Phantasie2Campaign.OrbId, bearer.InventoryItemIds); // the ST never takes the orb (§6 quirk)

        // The game goes on: the wands are handed out, each OK member uses one ("You are improved", SCORE 250).
        Assert.Equal(TownTransactionKind.Ok, s.DistributeLoot().Kind);
        foreach (var m in s.Party.Members)
        {
            Assert.Contains(Phantasie2Campaign.FilmonWandId, m.InventoryItemIds);
            var before = m.Stats;
            Assert.Equal("You are improved", s.UseCampaignItem(m, Phantasie2Campaign.FilmonWandId).Message);
            Assert.Equal(Phantasie2Campaign.BumpAll(before), m.Stats);
            Assert.Equal(Phantasie2Campaign.WandUsedScore, m.P2.QuestScore);
        }
        Assert.Contains("You are", s.ConsultMystic().Message); // rank 5 (SCORE > 150)
        Assert.Equal(5, Phantasie2Campaign.MysticScore(s.Party.Members).Rank);
        Assert.True(s.LeaveTown());
        d.Walk([N]);
        Assert.Equal(GameMode.Overworld, s.Mode);
    }

    // ==== 5. Party defeat ============================================================================================

    /// <summary>
    /// A wiped party is judged (I's Great Demon, II's UNDEAD line); survivors wake at OUT18 (18,14), else the game goes on
    /// in PIPPACOTT with an empty party. Seeds 1..12 (mixed levels, then two level-1 members) reach both outcomes.
    /// </summary>
    [OriginalDataFact]
    public void Stage5_PartyDefeat_SurvivorsWakeAtTheTemple_OrTheGameGoesHome()
    {
        var (woke, home) = (0, 0);
        for (var seed = 1; seed <= 12; seed++)
        {
            if (DefeatRun(seed))
                woke++;
            else
                home++;
        }
        Assert.True(woke > 0, "no seed revived anyone");
        Assert.True(home > 0, "every seed revived someone");
    }

    /// <summary>One party defeat; true when someone was revived.</summary>
    private static bool DefeatRun(int seed)
    {
        var (s, d) = Outside(seed, members: seed > 6 ? 2 : 6);
        for (var i = 0; i < s.Party.Members.Count; i++)
            s.Party.Members[i].Level = seed > 6 || i < 2 ? 1 : 2 + 2 * i; // level 1: undead 1 in 4, else destroyed (PartyDefeat.Judge)
        s.BeginWildernessCombat(0);
        foreach (var m in s.Party.Members)
        {
            m.HpCur = 0;
            m.Status = CharacterStatus.Dead;
        }
        s.ResolveCombatRound();
        Assert.True(s.PartyDefeatPending);
        var result = s.ResolvePartyDefeat();
        Assert.False(s.PartyDefeatPending);
        // II's UNDEAD line is in II's table; the test process reads I's table first (GameTextSetup), so the log shows I's.
        Assert.Contains("GRANTED UNDEAD STATUS", GameTextSetup.PhantasieII.Get("core.defeat.x_status_undead"));
        if (result.AnyRevived)
        {
            Assert.Equal(GameMode.Overworld, s.Mode);
            Assert.Equal((Phantasie2Campaign.WakeRegion, Phantasie2Campaign.WakeX, Phantasie2Campaign.WakeY),
                (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
            foreach (var m in s.Party.Members.Where(m => m.Status != CharacterStatus.Dead))
                m.HpCur = Math.Max(m.HpCur, 1);
            var moved = false;
            foreach (var dir in new[] { N, E, S, W })
            {
                if (s.Mode != GameMode.Overworld)
                    break;
                moved = s.MoveOverworld(dir).Kind != OverworldMoveKind.Blocked;
                d.Settle();
                if (moved)
                    break;
            }
            Assert.True(moved, "stuck at the bloodstained temple");
            return true;
        }
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(Phantasie2Campaign.HomeTownId, s.CurrentTownId);
        Assert.Empty(s.Party.Members);
        // Nobody left to play: the Guild makes a new party, which can leave town.
        s.OpenGuild();
        var fresh = s.RollNewMember(Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryEnrollNewMember(fresh, "PHOENIX", out var err), err);
        Assert.True(s.TryAddToParty(fresh, out err), err);
        s.LeaveGuild();
        Assert.True(s.LeaveTown());
        return false;
    }

    // ==== 6. Save and load in the middle of DNG1 =====================================================================

    [OriginalDataFact]
    public void Stage6_SaveInDng1_LoadAndWalkOut()
    {
        var (s, d) = Outside(seed: 11);
        d.Walk(PippacottToDng1);
        s.AnswerOverworldEntry(true);
        d.Step([S, S]);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var t = GameSessionFactory.Create(Data, new SeededDosRandomSource(12));
        t.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Same(Phantasie2Campaign.Instance, t.Campaign);
        Assert.Equal(GameMode.Dungeon, t.Mode);
        Assert.Equal((1, 15, 2), (t.Dungeon!.LevelId, t.Dungeon.X, t.Dungeon.Y));
        var e = new Driver(t);
        e.Step([N, N]);
        Assert.True(t.DungeonExitPending);
        t.AnswerDungeonExit(true, saveMap: true);
        Assert.Equal((2, 10, 2), (t.Overworld.RegionId, t.Overworld.X, t.Overworld.Y));
        Assert.Equal(1, t.DungeonSave!.DungeonId);
    }

    [OriginalDataTheory]
    [InlineData("manual", false)]
    [InlineData("st", true)]
    public void D2041_NoToTheMapSave_ForgetsTheSameDungeonsSlot_StKeepsIt(string value, bool kept)
    {
        var options = GameOptions.ForPreset(GameId.P2).With(RuleOptionRegistry.P2DungeonMapNo.Id, value);
        var (s, d) = Outside(seed: 11, options);
        d.Walk(PippacottToDng1);
        s.AnswerOverworldEntry(true);
        d.Step([S, S]);
        s.DungeonSave = new DungeonSaveSlot(1, new byte[DungeonLevel.BufferSize]); // an older YES save of DNG1
        d.Step([N, N]);
        Assert.True(s.DungeonExitPending);
        s.AnswerDungeonExit(true, saveMap: false);
        Assert.Equal(kept, s.DungeonSave is not null);
    }

    // ==== 7. Sweeps: every step on every map =======================================================================

    /// <summary>Back to full strength, everyone OK (the sweeps only look for throws and soft-locks).</summary>
    private static void Heal(GameSession s)
    {
        foreach (var m in s.Party.Members)
        {
            m.Status = CharacterStatus.Ok;
            m.HpCur = m.HpMax;
        }
    }

    private static void Record(Dictionary<string, string> failures, string where, Exception ex)
    {
        var key = ex.GetType().Name + ": " + ex.Message.Split('\n')[0];
        failures.TryAdd(key, where);
    }

    private static string Report(Dictionary<string, string> failures) =>
        string.Join("\n", failures.Select(f => $"{f.Value}: {f.Key}"));

    /// <summary>
    /// Every walkable cell of DNG1..DNG8, a step in each direction, every prompt answered (event menus with option 1,
    /// treasure kept, locks backed off, exits refused): nothing throws and nothing keeps asking.
    /// </summary>
    [OriginalDataFact]
    public void Sweep_EveryDungeonCell_EveryDirection_NothingThrows()
    {
        var failures = new Dictionary<string, string>();
        var kinds = new Dictionary<DungeonMoveKind, int>();
        var levels = Data.CreateDungeonTraversal();
        foreach (var levelId in levels.LevelIds.Order())
        {
            var (s, d) = Outside(seed: 100 + levelId);
            var level = levels.GetLevel(levelId);
            for (var y = 0; y < DungeonLevel.Height; y++)
            for (var x = 0; x < DungeonLevel.Width; x++)
            {
                if (!level.IsWalkable(x, y))
                    continue;
                foreach (var dir in new[] { N, E, S, W })
                {
                    var where = $"DNG{levelId} ({x},{y}) {dir}";
                    try
                    {
                        if (s.Mode == GameMode.Town && s.Party.Members.Count == 0)
                            (s, d) = Outside(seed: 100 + levelId * 1000 + y * 33 + x); // a wipe with nobody revived: a new party
                        if (s.Mode == GameMode.Town)
                            Assert.True(s.LeaveTown(), $"cannot leave town {s.CurrentTownId}, party {s.Party.Members.Count}: " + string.Join(" / ", s.Log.TakeLast(25)));
                        else if (s.Mode == GameMode.Dungeon && s.DungeonExitPending)
                            s.AnswerDungeonExit(false, false);
                        Heal(s);
                        s.EnterDungeon(levelId, x, y);
                        var k = s.MoveDungeon(dir).Kind;
                        kinds[k] = kinds.GetValueOrDefault(k) + 1;
                        d.Settle();
                        if (s.DungeonExitPending)
                            s.AnswerDungeonExit(false, false);
                        Assert.True(s.Mode is GameMode.Dungeon or GameMode.Overworld or GameMode.Town, $"mode {s.Mode}");
                    }
                    catch (Exception ex)
                    {
                        Record(failures, where, ex);
                        (s, d) = Outside(seed: 100 + levelId); // start over on a clean session
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, Report(failures));
        Assert.True(kinds.GetValueOrDefault(DungeonMoveKind.ScriptedEvent) >= 100 && kinds.GetValueOrDefault(DungeonMoveKind.TriggerCombat) >= 20, string.Join(", ", kinds));
    }

    /// <summary>
    /// Every non-wall cell of II's 17 overworld regions, a step in each direction (crossing region edges, towns and
    /// dungeon entrances refused, inns, swims, lava, mist and fixed fights): nothing throws and nothing keeps asking.
    /// </summary>
    [OriginalDataFact]
    public void Sweep_EveryOverworldCell_EveryDirection_NothingThrows()
    {
        var failures = new Dictionary<string, string>();
        var kinds = new Dictionary<OverworldMoveKind, int>();
        var (s, d) = Outside(seed: 300);
        var traversal = Data.CreateTraversal(Phantasie2Campaign.Instance);
        foreach (var region in Data.OverworldRegions.Select(r => r.RegionId).Order())
        {
            var map = traversal.GetRegion(region);
            for (var y = 0; y < OverworldRegion.Height; y++)
            for (var x = 0; x < OverworldRegion.Width; x++)
            {
                if (map.GetNormalized(x, y) == OverworldRegion.WallTile)
                    continue;
                foreach (var dir in new[] { N, E, S, W })
                {
                    var where = $"OUT{region} ({x},{y}) {dir}";
                    try
                    {
                        if (s.Mode == GameMode.Town && s.Party.Members.Count == 0)
                            (s, d) = Outside(seed: 300 + region * 1000 + y * 26 + x); // a wipe with nobody revived: a new party
                        if (s.Mode == GameMode.Town)
                            Assert.True(s.LeaveTown(), $"cannot leave town {s.CurrentTownId}, party {s.Party.Members.Count}: " + string.Join(" / ", s.Log.TakeLast(25)));
                        else if (s.Mode == GameMode.Dungeon)
                            s.ExitDungeonToWilderness();
                        Heal(s);
                        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (region, x, y);
                        var k = s.MoveOverworld(dir).Kind;
                        kinds[k] = kinds.GetValueOrDefault(k) + 1;
                        d.Settle();
                    }
                    catch (Exception ex)
                    {
                        Record(failures, where, ex);
                        (s, d) = Outside(seed: 300);
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, Report(failures));
        Assert.True(kinds.GetValueOrDefault(OverworldMoveKind.EnterDungeon) >= 20 && kinds.GetValueOrDefault(OverworldMoveKind.EnterTown) >= 30, string.Join(", ", kinds));
    }

    // ==== 8. On foot inside the dungeons: entrance → the orb, entrance → the Ice Dragon ===============================

    /// <summary>Prints the explorer's event steps and their text (debugging only; tests run in parallel).</summary>
    private static readonly bool Trace = false;

    /// <summary>How often the explorer steps onto one event cell again (menus cycle their options; random teleports need many goes).</summary>
    private const int MaxEventRetries = 30;

    /// <summary>How often the explorer forgets its barred moves (bounds the retry loop).</summary>
    private const int MaxClears = 3000;

    /// <summary>Cells worth stepping onto again: scripted events and locks (another pick may open them).</summary>
    private static bool Retriable(DungeonLevel level, (int X, int Y) c) =>
        DungeonLevel.Classify(level.Get(c.X, c.Y)) is DungeonFeatureKind.ScriptedEvent or DungeonFeatureKind.ScriptLow
            or DungeonFeatureKind.LockOrAmbush;

    /// <summary>
    /// Explores the live level on foot until the party stands on (<paramref name="tx"/>, <paramref name="ty"/>): a
    /// breadth-first search over non-wall cells, re-planned after every step, heads for the target when it is reachable
    /// and otherwise for the nearest cell not stepped on yet (events there may open walls or move the party). A step
    /// that leaves the party where it was (a pushback, a refused lock) bars that move until the next retry round, in
    /// which event and lock cells are stepped onto again. Prompts are settled boldly (<see cref="Driver.Bold"/>).
    /// Returns whether it got there, with the step count or why not.
    /// </summary>
    private static (bool Ok, string Why) WalkDungeonTo(GameSession s, Driver d, int tx, int ty, int maxSteps = 20000)
    {
        d.Bold = true;
        foreach (var m in s.Party.Members) // explorers that do not die: the walk tests the map, not the fights
        {
            m.HpMax = 30_000;
            m.Skills = new CharacterSkills(120, 90, 90, 90, 90, 90, 95, 90); // a master thief opens every lock
            m.WeaponDamageA = 120;
        }
        var barred = new HashSet<((int, int), (int, int))>();
        var visited = new HashSet<(int, int)>();
        var retries = new Dictionary<(int, int), int>();
        (int, int)? retryTarget = null;
        var clears = 0;
        var dirs = new[] { N, E, S, W };
        for (var step = 0; step < maxSteps; step++)
        {
            if (s.Mode != GameMode.Dungeon)
                return (false, $"left the dungeon ({s.Mode}) after {step} steps: " + string.Join(" / ", s.Log.TakeLast(12)));
            var here = (s.Dungeon!.X, s.Dungeon.Y);
            visited.Add(here);
            if (here == retryTarget)
                retryTarget = null;
            if (here == (tx, ty))
                return (true, $"{step} steps");
            var level = s.CampaignAccess.CurrentDungeonLevel;
            var prev = new Dictionary<(int, int), ((int, int) From, CardinalDirection Dir)> { [here] = (here, N) };
            var q = new Queue<(int X, int Y)>();
            q.Enqueue(here);
            (int, int)? goal = null;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var dir in dirs)
                {
                    var n = dir switch
                    {
                        N => (c.X, c.Y - 1), S => (c.X, c.Y + 1), E => (c.X + 1, c.Y), _ => (c.X - 1, c.Y),
                    };
                    if (prev.ContainsKey(n) || barred.Contains((c, n)) || !level.IsWalkable(n.Item1, n.Item2))
                        continue;
                    prev[n] = (c, dir);
                    q.Enqueue(n);
                    if (goal is null && !visited.Contains(n))
                        goal = n; // nearest unexplored cell
                }
            }
            if (prev.ContainsKey((tx, ty)))
                goal = (tx, ty);
            if (goal is null && retryTarget is { } rt && prev.ContainsKey(rt))
                goal = rt;
            if (goal is null && barred.Count > 0 && clears++ < MaxClears)
            {
                barred.Clear(); // before a retry round, give barred cells (pushbacks, locks) another go
                continue;
            }
            if (goal is null)
            {
                // Everything reachable is seen: step onto an event or lock cell again (menus answer the next option in
                // turn, a lock gets another pick). Standing on it: step off first, the target stays.
                foreach (var c in prev.Keys)
                {
                    if (retries.GetValueOrDefault(c) >= MaxEventRetries || !Retriable(level, c))
                        continue;
                    retries[c] = retries.GetValueOrDefault(c) + 1;
                    retryTarget = c;
                    goal = c != here ? c : prev.Keys.FirstOrDefault(n => n != here && prev[n].From == here);
                    if (goal == default((int, int)) && c == here)
                        goal = null;
                    break;
                }
            }
            if (goal is not { } g)
                return (false, $"dead end at {here} after {step} steps ({visited.Count} cells seen; events tried: "
                               + string.Join(" ", retries.Select(r => $"{r.Key}x{r.Value}")) + "; reachable events: "
                               + string.Join(" ", prev.Keys.Where(c => level.Get(c.Item1, c.Item2) >= 0xdd).Select(c => $"{c}=0x{level.Get(c.Item1, c.Item2):x2}")) + "): "
                               + string.Join(" / ", s.Log.TakeLast(12)));
            var cell = g;
            while (prev[cell].From != here)
                cell = prev[cell].From;
            var seq = s.LogSequence;
            var mv = s.MoveDungeon(prev[cell].Dir);
            if (cell == retryTarget)
                retryTarget = null;
            if (Trace && mv.Kind is DungeonMoveKind.ScriptedEvent or DungeonMoveKind.TriggerStairs or DungeonMoveKind.TriggerMessage && mv.FeatureByte < 0x8b || Trace && mv.FeatureByte >= 0xb9)
                Console.WriteLine($"{here}->{cell} {mv.Kind} 0x{mv.FeatureByte:x2}");
            d.Settle();
            if (s.DungeonExitPending)
                s.AnswerDungeonExit(false, false);
            if (s.Mode == GameMode.Dungeon && (s.Dungeon!.X, s.Dungeon.Y) == here)
            {
                barred.Add((here, cell)); // until the next retry round
                visited.Add(cell); // an event that pushes back has run; don't make it the goal again
            }
            if (Trace)
                foreach (var l in s.LogSince(seq).Where(l => !l.Contains(" HITS") && !l.Contains("MISSES") && !l.Contains("TAKES") && !l.Contains("DIES")))
                    Console.WriteLine("   " + l);
            Heal(s);
        }
        return (false, $"not there after {maxSteps} steps ({visited.Count} cells seen)");
    }

    [OriginalDataFact]
    public void Stage8_Dng5_OnFoot_FromTheEntrance_ToTheOrb()
    {
        var (s, d) = Outside(seed: 55);
        s.EnterDungeon(5, 16, 0); // OUT17 (20,5)'s dest
        var (ok, why) = WalkDungeonTo(s, d, 16, 32);
        Assert.True(ok, why);
        s.MoveDungeon(S);
        d.Settle();
        Assert.Contains(Phantasie2Campaign.OrbId, s.Sack.Items);
    }

    [OriginalDataFact]
    public void Stage8_Dng8_OnFoot_FromTheEntrance_ToTheIceDragon()
    {
        var (s, d) = Outside(seed: 88);
        foreach (var m in s.Party.Members)
            m.P2.QuestScore = Phantasie2Campaign.DragonGateScore;
        s.EnterDungeon(8, 19, 0); // OUT30 (15,5)'s dest
        // Through the six-button teleporters (random) to the door north of the dragon's hall.
        var (ok, why) = WalkDungeonTo(s, d, 22, 33);
        Assert.True(ok, why);
        d.Bold = false;

        if (s.CampaignAccess.CurrentDungeonLevel.Get(22, 34) != 0x8e) // the explorer may have pressed the buttons
            OpenTheNineButtonDoor(s);
        (ok, why) = WalkDungeonTo(s, d, 32, 32);
        Assert.True(ok, why);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, s.MoveDungeon(S).Kind);
        Assert.Equal(["LEAVE", "APPROACH"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
    }

    /// <summary>
    /// The door (22,34), from (22,33): "A SET OF NINE BUTTONS" → PUSH ONE → button 8 (record byte 9 = script 90 → event
    /// 12) → "PUSH BUTTON?" YES → button 3 → "THE DOOR OPENS" (the cell becomes 0x8e). Before s82 the 9-button menu was
    /// decoded with I's label rule as two options, so button 8 could not be pressed: the dragon was unreachable.
    /// </summary>
    private static void OpenTheNineButtonDoor(GameSession s)
    {
        var level = s.CampaignAccess.CurrentDungeonLevel;
        Assert.Equal((22, 33), (s.Dungeon!.X, s.Dungeon.Y));
        Assert.Equal(DungeonMoveKind.ScriptedEvent, s.MoveDungeon(S).Kind);
        Assert.Equal(["PUSH ONE", "LEAVE"], s.DungeonEventPrompt!.Message.Options);
        s.AnswerDungeonEvent(1);
        Assert.Equal(9, s.DungeonEventPrompt!.Message.Options.Count);
        s.AnswerDungeonEvent(8);
        Assert.Equal(["YES", "NO"], s.DungeonEventPrompt!.Message.Options);
        s.AnswerDungeonEvent(1);
        Assert.Equal(9, s.DungeonEventPrompt!.Message.Options.Count);
        s.AnswerDungeonEvent(3);
        Assert.Contains(s.Log, l => l.Contains("THE DOOR OPENS"));
        Assert.Equal(0x8e, level.Get(22, 34));
    }

    /// <summary>The door alone, from a prepared jump to (22,33): independent of the explorer's random teleports.</summary>
    [OriginalDataFact]
    public void Stage8_Dng8_NineButtonDoor_Opens_AndLeadsToTheDragon()
    {
        var (s, d) = Outside(seed: 89);
        foreach (var m in s.Party.Members)
            m.P2.QuestScore = Phantasie2Campaign.DragonGateScore;
        s.EnterDungeon(8, 22, 33);
        OpenTheNineButtonDoor(s);
        var (ok, why) = WalkDungeonTo(s, d, 32, 32);
        Assert.True(ok, why);
        Assert.Equal(DungeonMoveKind.ScriptedEvent, s.MoveDungeon(S).Kind);
        Assert.Equal(["LEAVE", "APPROACH"], s.DungeonEventPrompt!.Message.Options.Select(o => o.Trim()));
    }

    // ==== 9. Data invariant behind the DNG8 door ======================================================================

    /// <summary>
    /// Every event a party can meet (on the grid, or a GOTO EVENT target of their scripts, following JUMP / RANDJUMP)
    /// offers as many menu options as its record's byte 1 says (no menu when it is 0 or 1). I's label rule had cut DNG8's
    /// nine-button menu (msg 71) to two options and blanked msg 76's labels.
    /// </summary>
    [OriginalDataFact]
    public void Data_EveryLiveEventMenu_HasItsRecordsOptionCount()
    {
        var levels = Data.CreateDungeonTraversal();
        var bad = new List<string>();
        var checkedCount = 0;
        foreach (var id in levels.LevelIds.Order())
        {
            var level = levels.GetLevel(id);
            foreach (var f in LiveEvents(level))
            {
                var rec = DungeonScript.EventRecordOffset(f);
                var r0 = level.GetBufferByte(rec);
                if (r0 is 0 or DungeonScript.SpentRecord)
                    continue;
                var count = level.GetBufferByte(rec + 1);
                var options = level.Messages.TryGetValue(r0 & 0x7f, out var m) ? m.Options : [];
                checkedCount++;
                if (options.Count != (count >= 2 ? count : 0) || options.Any(o => o.Trim().Length == 0))
                    bad.Add($"DNG{id} 0x{f:x2} msg {r0 & 0x7f}: record {count}, options [{string.Join("|", options)}]");
            }
        }
        Assert.True(checkedCount > 100, $"only {checkedCount} events checked");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    private static IEnumerable<byte> LiveEvents(DungeonLevel level)
    {
        var todo = new Stack<int>();
        for (var y = 0; y < DungeonLevel.Height; y++)
        for (var x = 0; x < DungeonLevel.Width; x++)
            if (level.Get(x, y) >= DungeonScript.FirstEventFeature)
                todo.Push(level.Get(x, y));
        var seen = new SortedSet<int>();
        var scripts = new HashSet<int>();
        while (todo.Count > 0)
        {
            var f = todo.Pop();
            if (f > 0xff || !seen.Add(f))
                continue;
            var rec = DungeonScript.EventRecordOffset((byte)f);
            var work = new Stack<int>();
            for (var c = 1; c <= Math.Max((int)level.GetBufferByte(rec + 1), 1); c++)
                work.Push(level.GetBufferByte(rec + c + 1));
            while (work.Count > 0)
            {
                var sid = work.Pop();
                if (!scripts.Add(sid))
                    continue;
                var p = DungeonScript.ScriptBase + 2 * sid;
                for (var n = 0; n < Phantasie2DungeonScript.MaxOps; n++)
                {
                    int o = level.GetBufferByte(p), a = level.GetBufferByte(p + 1), b = level.GetBufferByte(p + 2);
                    var op = o % 128;
                    if (op > 0x3f)
                        op -= 0x40;
                    if (op == 22) todo.Push(a + 0xdc);
                    else if (op == 20) work.Push(a);
                    else if (op == 21) work.Push(b);
                    if (o <= 0x7f)
                        break;
                    p += Phantasie2DungeonScript.IsFourByte(op) || o == Phantasie2DungeonScript.SpentFourByteOp ? 4 : 2;
                }
            }
        }
        return seen.Select(f => (byte)f);
    }
}
