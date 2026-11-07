using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's 7 × 7 battle, s102 leftovers (<c>combat.md</c> §8, §11, §14): where the party goes after a flight, Beg
/// or Teleportation (<see cref="P4FightReturn"/>, D4-052 <c>p4.teleport</c>, D4-070).
/// </summary>
public class Phantasie4BattleLeftoversTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();

    internal static P4Party Party(int members = 2, int level = 5, int cls = P4Ids.Fighter)
    {
        var p = new P4Party { Gold = 5000, AverageLevel = level };
        for (var i = 0; i < members; i++)
        {
            var m = new P4Member
            {
                Name = $"M{i}", Race = P4Ids.Human, Class = cls, Level = level, HpMax = 60, Hp = 60, MpMax = 10, Mp = 10,
                Abilities = [15, 15, 15, 15, 15, 15],
            };
            Array.Fill(m.Skills, 60);
            m.Items[0] = 0x1C + 4;
            m.MoveBase = P4Creation.Stride(Manual, T.Creation, m.Race, m.Class); // +29 from creation
            m.MeleeBonus = P4TownRules.MeleeBonus(m);                           // +3B from the last town exit
            p.Members.Add(m);
        }
        return p;
    }

    // --- Where the party goes after a fight (_enc_end, _oencgo) -------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(P4BattleEnd.Fled, true, P4FightReturnKind.Previous)]
    [InlineData(P4BattleEnd.Begged, true, P4FightReturnKind.Previous)]
    [InlineData(P4BattleEnd.Teleported, true, P4FightReturnKind.Previous)]
    [InlineData(P4BattleEnd.Greeted, true, P4FightReturnKind.Stay)]       // 106 jumps past the _cox copy
    [InlineData(P4BattleEnd.AllKilled, true, P4FightReturnKind.Stay)]
    [InlineData(P4BattleEnd.Surrendered, true, P4FightReturnKind.Stay)]
    [InlineData(P4BattleEnd.Fled, false, P4FightReturnKind.Stay)]         // _cox is outdoor-only: a location map keeps the square
    [InlineData(P4BattleEnd.Begged, false, P4FightReturnKind.Stay)]
    [InlineData(P4BattleEnd.Teleported, false, P4FightReturnKind.Previous)] // D4-070 (user s103): previous square is the default
    public void FightReturn_ByEnd(P4BattleEnd end, bool outdoors, P4FightReturnKind expected)
    {
        Assert.Equal(expected, P4FightReturn.For(Manual, end, outdoors));
        Assert.Equal(expected, P4FightReturn.For(Code, end, outdoors));
    }

    [OriginalDataTheory]
    [InlineData("random", true, P4FightReturnKind.Random)]
    [InlineData("random", false, P4FightReturnKind.Random)]
    [InlineData("entrance", false, P4FightReturnKind.Entrance)]
    [InlineData("entrance", true, P4FightReturnKind.Previous)] // no entrance outdoors [~]
    [InlineData("stay", true, P4FightReturnKind.Previous)]     // D4-070: the code's _cox copy, outdoors only
    [InlineData("stay", false, P4FightReturnKind.Stay)]
    public void FightReturn_TeleportOption(string value, bool outdoors, P4FightReturnKind expected)
    {
        var o = Manual.With(P4RuleOptions.Teleport.Id, value);
        Assert.Equal(expected, P4FightReturn.For(o, P4BattleEnd.Teleported, outdoors));
        // The option only moves Teleportation: a flight still goes back (outdoors) or stays (location map).
        Assert.Equal(outdoors ? P4FightReturnKind.Previous : P4FightReturnKind.Stay, P4FightReturn.For(o, P4BattleEnd.Fled, outdoors));
    }

    [OriginalDataFact]
    public void RandomSquares_AreOpenLandOrOpenFloor()
    {
        var rng = new SeededRandomSource(3);
        foreach (var (_, map) in Atlas.Outdoors)
        {
            if (P4FightReturn.RandomOutdoorSquare(map, B.Tables, rng) is not var (x, y))
                continue;
            var f = B.Tables.FieldFor(P4OutdoorMap.TypeOf(map.Tile(x, y)));
            Assert.NotNull(f);
            Assert.NotEqual("ship", f!.Name);
        }
        var files = Phantasie4World.LoadDungeonFiles();
        foreach (var (_, file) in files)
        {
            var m = new P4DungeonMap(file);
            if (P4FightReturn.RandomDungeonSquare(m, rng) is not var (dx, dy))
                continue;
            Assert.False(m.FightRefused(dx, dy));
            Assert.Contains(Enumerable.Range(0, 4), d => m.Move(dx, dy, d) == P4DungeonMap.Open);
        }
    }

    /// <summary>The first outdoor square (from map 1) whose 3 × 3 block is all grassland.</summary>
    private static P4Location Grass()
    {
        foreach (var (n, m) in Atlas.Outdoors.OrderBy(e => e.Key))
            for (var y = 2; y < 24; y++)
                for (var x = 2; x < 24; x++)
                    if (Enumerable.Range(-1, 3).All(dy => Enumerable.Range(-1, 3)
                            .All(dx => P4OutdoorMap.TypeOf(m.Tile(x + dx, y + dy)) == 6)))
                        return new P4Location(P4Place.Outdoors, n, x, y);
        throw new InvalidOperationException("no grass");
    }

    /// <summary>Walks east / west on grass until a fight starts; null when none comes.</summary>
    private static Phantasie4Campaign? CampaignAtFight(GameOptions o, int seed)
    {
        var files = Phantasie4World.LoadDungeonFiles();
        var c = new Phantasie4Campaign(o, T, Atlas, Party(members: 4, level: 8), new SeededRandomSource(seed),
            n => files[n], id => $"#{id}", Grass(), B);
        c.Start();
        for (var step = 0; step < 3000 && !c.PartyLost; step++)
        {
            if (c.Battle is { Prompt: P4BattlePrompt.Options })
                return c;
            if (c.Battle is not null || c.Place != P4Place.Outdoors)
                return null;
            switch (c.Outdoor.Prompt)
            {
                case P4OutdoorPrompt.Action: c.Outdoor.Move(step % 2 == 0 ? 4 : 3); break;
                case P4OutdoorPrompt.Wait: c.Outdoor.Continue(); break;
                case P4OutdoorPrompt.Confirm: c.Outdoor.Answer(true); break;
                default: return null;
            }
            c.Follow();
        }
        return null;
    }

    [OriginalDataFact]
    public void Campaign_BegAccepted_PutsThePartyBackOnTheOldSquare()
    {
        var checkedOne = false;
        for (var seed = 1; seed <= 40 && !checkedOne; seed++)
        {
            var c = CampaignAtFight(Manual, seed);
            if (c is null)
                continue;
            var old = (c.Outdoor.OldX, c.Outdoor.OldY);
            var here = (c.Outdoor.X, c.Outdoor.Y);
            if (old == here)
                continue;
            var b = c.Battle!;
            b.ChooseOption(P4EncounterOption.Beg);
            if (b.End != P4BattleEnd.Begged)
                continue;
            while (b.Prompt == P4BattlePrompt.Treasure)
                b.TakeItem(false);
            b.Continue();
            c.Follow();
            Assert.Null(c.Battle);
            Assert.Equal(old, (c.Outdoor.X, c.Outdoor.Y));
            checkedOne = true;
        }
        Assert.True(checkedOne, "no accepted Beg met");
    }

    [OriginalDataFact]
    public void Campaign_GreetAccepted_StaysOnTheFightSquare()
    {
        var checkedOne = false;
        for (var seed = 1; seed <= 60 && !checkedOne; seed++)
        {
            var c = CampaignAtFight(Manual, seed);
            if (c is null)
                continue;
            var here = (c.Outdoor.X, c.Outdoor.Y);
            if ((c.Outdoor.OldX, c.Outdoor.OldY) == here)
                continue;
            var b = c.Battle!;
            b.ChooseOption(P4EncounterOption.Greet);
            if (b.End != P4BattleEnd.Greeted)
                continue;
            b.Continue();
            c.Follow();
            Assert.Equal(here, (c.Outdoor.X, c.Outdoor.Y));
            checkedOne = true;
        }
        Assert.True(checkedOne, "no accepted Greet met");
    }

    // --- Pathing and monster targets (_b_amove, _b_chauto, _obj_select, breath) --------------------------------------

    private sealed class Low : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    private sealed class High : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    /// <summary>Keeps only the first monster on the field (rand = 1 brings follow-up groups).</summary>
    private static P4BattleMonster Alone(P4Battle b)
    {
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        return b.Monsters[0];
    }

    [OriginalDataFact]
    public void MonsterWalk_GoesStraightDownTheColumn_AndStopsNextToItsTarget()
    {
        const int skeleton = 0;
        var p = Party(members: 1);
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), skeleton);
        var mon = Alone(b);
        Assert.Equal(3, mon.Square);
        b.Members[0].Square = 45;          // straight below, row 6
        b.ChooseOption(P4EncounterOption.Fight);
        b.Order(new P4Order(P4ActionKind.Parry));
        // Each step takes the neighbour nearest the target (S, index 7 of _base); the occupied target ends the walk.
        var mv = B.Monsters[skeleton].Movement;
        Assert.Equal(3 + 7 * Math.Min(mv, 5), mon.Square);
    }

    [OriginalDataFact]
    public void Breath_ScansWideningRings_AndHitsOneMoreThanItsCount()
    {
        const int babyDragon = 30;          // spell ids [66, 0, 0]
        var p = Party(members: 1);
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), babyDragon);
        var mon = Alone(b);
        b.Members[0].Square = mon.Square + P4Battle.Size; // next to it: found on rings 1, 2 and 3
        b.ChooseOption(P4EncounterOption.Fight);
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(60 - 3, p.Members[0].Hp); // three hits of rand(rand(16) + 9) − A, floored to 1
    }

    [OriginalDataFact]
    public void Flee_WalksTowardTheExit_ThenAsksForOrders()
    {
        var p = Party(members: 1);
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        var exit = b.Exits.Single();
        var before = P4Battle.Range2(b.Members[0].Square, exit);
        b.ChooseOption(P4EncounterOption.Flee);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        Assert.True(P4Battle.Range2(b.Members[0].Square, exit) < before);
    }

    // --- Shock Wave (_shoot / _xwave, _pmagic2) -----------------------------------------------------------------------

    private static List<int> Squares(IEnumerable<int> path)
    {
        var list = new List<int>();
        foreach (var sq in path)
            if (list.Count == 0 || list[^1] != sq)
                list.Add(sq);
        return list;
    }

    [Theory]
    [InlineData(45, 3, new[] { 45, 38, 31, 24, 17, 10, 3 })]   // straight up the column
    [InlineData(24, 27, new[] { 24, 25, 26, 27 })]             // along a row
    [InlineData(48, 0, new[] { 48, 47, 40, 39, 32, 31, 24, 23, 16, 15, 8, 7, 0 })] // a diagonal stairs (squares are 6 × 5 cells)
    [InlineData(45, 2, new[] { 45, 38, 31, 24, 17, 16, 9, 2 })] // a slanted line steps sideways mid-way
    [InlineData(10, 10, new int[0])]                           // on oneself: no point
    public void WavePath_IsTheBresenhamLineOnTheCellGrid(int from, int to, int[] expected) =>
        Assert.Equal(expected, Squares(P4Battle.WavePath(from, to)));

    [OriginalDataFact]
    public void ShockWave_HitsFriendAndFoeOnTheLine()
    {
        var p = Party(members: 2);
        var caster = p.Members[0];
        caster.Spells[50] = 1;              // Shock Wave 1 (51)
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        var mon = Alone(b);
        var hp0 = mon.Hp;
        b.Members[0].Square = 45;
        b.Members[1].Square = 38;           // in the way
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Contains(51, b.CastableSpells());
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, mon.Square, 51)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.True(p.Members[1].Hp <= 60 - 7);
        Assert.True(mon.Dead || mon.Hp == hp0 - 7);
        Assert.Equal(60, caster.Hp);
    }

    [OriginalDataFact]
    public void Spell64_IsOfferedInCombat_AndOnlyEmptiesMp()
    {
        Assert.True(P4Battle.CombatSpellBuilt(64));
        Assert.False(P4Battle.CombatSpellBuilt(63));   // dungeon page only
        Assert.False(P4Battle.CombatSpellBuilt(65));   // town page only
        var p = Party(members: 1);
        p.Members[0].Spells[63] = 1;
        p.Members[0].Mp = p.Members[0].MpMax = 30;
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        var mon = Alone(b);
        var hp0 = mon.Hp;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Contains(64, b.CastableSpells());
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, -1, 64)));
        Assert.Equal(0, p.Members[0].Mp);
        Assert.Equal(hp0, mon.Hp);
    }

    [OriginalDataFact]
    public void Spell64_Divine_HitsEveryMonsterFor2MToFourMMinus1_ThenEmptiesMp()
    {
        Assert.Equal("x68000", GameOptions.ForPreset(GameId.P4, OptionPreset.Remaster).Get(P4RuleOptions.Annihilate.Id));
        var p = Party(members: 1);
        p.Members[0].Spells[63] = 1;
        p.Members[0].Mp = p.Members[0].MpMax = 5;
        var o = Manual.With(P4RuleOptions.Annihilate.Id, "divine");
        var b = P4Battle.Dungeon(o, T, B, p, new Low(), 0);
        var mon = Alone(b);
        mon.Hp = 500;
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, -1, 64)));
        Assert.Equal(0, p.Members[0].Mp);
        Assert.Equal(500 - 10, mon.Hp);   // M = 5: 2 × 5 + rand(10) − 1, the low source rolls 1
    }

    // --- Auto mode (_partyact, _eauto1, _try_attack, M) -----------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(0, 3, 0, P4EncounterOption.Greet)]    // friendly, round 1
    [InlineData(1, 3, 0, P4EncounterOption.Fight)]    // hostile
    [InlineData(2, 3, 0, P4EncounterOption.Beg)]      // surrender
    [InlineData(3, 3, 0, P4EncounterOption.Demand)]   // demand
    [InlineData(1, 0, 31, P4EncounterOption.Flee)]    // 120 × 75 % = 90 > 89 left
    [InlineData(1, 0, 30, P4EncounterOption.Fight)]   // 90 > 90 fails
    [InlineData(1, 3, 59, P4EncounterOption.Fight)]   // "fight to the end" never flees
    public void PartyStrategy_IsTheAttitude_ThenTheFlightTest(int attitude, int flight, int hpLost, P4EncounterOption expected)
    {
        var p = Party(members: 2);
        p.StoryFlags[0x3E] = (byte)attitude;
        p.StoryFlags[0x3F] = (byte)flight;
        p.Members[0].Hp -= hpLost / 2;
        p.Members[1].Hp -= hpLost - hpLost / 2;
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        Assert.Equal(expected, b.PartyStrategy());
    }

    [OriginalDataFact]
    public void AutoRound_GivesEveryMemberOrders_AndAsksAgain()
    {
        var p = Party(members: 3);
        p.StoryFlags[0x3E] = 1;             // hostile: Fight
        p.StoryFlags[0x3F] = 3;
        var b = P4Battle.Dungeon(Manual, T, B, p, new SeededRandomSource(4), 0);
        b.ChooseOption(P4EncounterOption.AutoRound);
        Assert.NotEqual(P4BattlePrompt.Order, b.Prompt);
        Assert.False(b.AutoCombat);
        Assert.True(b.Done || b.Prompt != P4BattlePrompt.Order);
    }

    [OriginalDataTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void AutoCombat_FightsToAnEnd(int seed)
    {
        foreach (var o in new[] { Manual, Code })
        {
            var p = Party(members: 4, level: 8);
            p.StoryFlags[0x3E] = 1;
            foreach (var m in p.Members)
                m.Strategy = [1, 0, 2, 3, 0];  // light defence, bow, thrust, close in, always attack
            var b = P4Battle.Dungeon(o, T, B, p, new SeededRandomSource(seed), seed * 5 % 40);
            for (var guard = 0; guard < 300 && !b.Done; guard++)
            {
                switch (b.Prompt)
                {
                    case P4BattlePrompt.Options: b.ChooseOption(P4EncounterOption.AutoCombat); break;
                    case P4BattlePrompt.Order: b.AutoMove(); break;
                    case P4BattlePrompt.Treasure: b.TakeItem(true); break;
                    case P4BattlePrompt.Over: b.Continue(); break;
                }
            }
            var onField = b.Members.Where(m => m.OnField).Select(m => m.Square)
                .Concat(b.Monsters.Where(m => m.OnField).Select(m => m.Square)).ToList();
            Assert.Equal(onField.Count, onField.Distinct().Count());
            Assert.True(b.Done || b.Round >= 100, $"seed {seed}: round {b.Round} {b.Prompt}");
        }
    }

    [OriginalDataFact]
    public void AutoMove_ClosesWithTheEnemy_AndPicksTheMeleeOfStrategyC()
    {
        var p = Party(members: 1);
        p.Members[0].Strategy = [0, 0, 2, 3, 2];  // no defence, bow, thrust, close in, no magic
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        var mon = Alone(b);
        b.ChooseOption(P4EncounterOption.Fight);
        var m = b.Current!;
        var before = P4Battle.Range2(m.Square, mon.Square);
        b.AutoMove();
        Assert.True(P4Battle.Range2(m.Square, mon.Square) < before || mon.Dead);
    }

    [OriginalDataTheory]
    [InlineData(24, true)]  // standing still: the memory holds, _atklist reads it at square 0 (list position 0)
    [InlineData(0, false)]  // the member moved since the last call: the memory is cleared, the target is random
    public void AutoAttack_AtkListMemory_TargetsTheListPositionAsASquare(int memorySquare, bool slip)
    {
        // _try_attack / _atklist [C]: memory by square, looked up by list position (original slip).
        var p = Party(members: 1);
        p.Members[0].Strategy = [0, 0, 2, 3, 0];  // no defence, bow, thrust, close in, no magic
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        var mon = Alone(b);
        b.ChooseOption(P4EncounterOption.Fight);
        var m = b.Current!;
        m.Square = 24;
        mon.Square = 17;                     // next to him
        m.AttackMemory[0] = 2;               // "hit at square 0" from earlier rounds
        m.MemorySquare = memorySquare;
        b.AutoMove();
        Assert.Equal(24, m.Square);          // movement D = 3 next to a monster: stays
        var shouted = b.Log.Any(l => l.StartsWith(p.Members[0].Name + ":", StringComparison.Ordinal));
        Assert.Equal(!slip, shouted);
        Assert.Equal(slip ? -2 : -1, m.AttackMemory[0]); // nobody on square 0: _chrexe writes −2
    }

    [OriginalDataFact]
    public void AutoCast_HealsInPartyOrder_OnlyAnOkayMemberBelowHalfHp()
    {
        // _try_cast [C]: the asleep M0 is passed over (condition must be Okay); M1 (Okay, 10 < 60 >> 1) gets Healing 1.
        var p = Party(members: 3);
        foreach (var x in p.Members)
            x.Strategy = [0, 0, 2, 0, 2];        // E = 2: never casts
        (p.Members[0].Hp, p.Members[0].Condition) = (10, P4Member.Asleep);
        p.Members[1].Hp = 10;
        var healer = p.Members[2];
        healer.Strategy = [0, 0, 2, 0, 4];       // E = 4: healing first
        healer.Spells[0] = 1;                    // Healing 1
        var b = P4Battle.Dungeon(Manual, T, B, p, new High(), 0);
        Alone(b);
        b.ChooseOption(P4EncounterOption.Fight);
        b.AutoMove();
        Assert.Equal(new P4Order(P4ActionKind.Cast, 1, 1), b.Members[2].Order);
    }

    [OriginalDataFact]
    public void Strategy_IsSaved()
    {
        var m = Party(members: 1).Members[0];
        Assert.Equal([2, 0, 2, 3, 3], m.Strategy);
        m.Strategy = [1, 1, 4, 5, 0];
        P4Battle.SetSupport(m, 3);
        var back = P4MemberSnapshot.From(m).ToMember();
        Assert.Equal(m.Strategy, back.Strategy);
        Assert.Equal(0x80 | 0x30 | 5, back.Strategy[3]);
    }

    // --- Summon Elemental (_pmagic2 42, _enc_end) ------------------------------------------------------------------------

    [OriginalDataFact]
    public void SummonElemental_JoinsAsSlotSix_ActsByItself_AndIsGoneAfterTheFight()
    {
        Assert.Equal(["Mud El.", "Mist El.", "Air El."], B.Elementals!.Select(e => e.Name));
        var p = Party(members: 2);
        p.Elemental = 1;                     // Mist El. added at the guild
        var caster = p.Members[0];
        caster.Spells[41] = 1;
        caster.Mp = caster.MpMax = 30;
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        Alone(b);
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.Equal(P4Battle.SpellTarget.Square, b.TargetOf(42));
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 24, 42)));
        b.Order(new P4Order(P4ActionKind.Parry));
        var el = Assert.Single(b.Members, m => m.Elemental);
        Assert.Equal(6, el.Slot);
        Assert.Equal(24, el.Square);
        Assert.Equal("Mist El.", el.Member.Name);
        Assert.Equal(200, el.Member.HpMax);
        Assert.Equal(2, p.Members.Count);   // never part of the saved party
        if (!b.Done && b.Prompt == P4BattlePrompt.Options)
        {
            b.ChooseOption(P4EncounterOption.Fight);
            Assert.NotSame(el, b.Current);   // the elemental takes no manual orders
        }
    }

    [OriginalDataFact]
    public void SummonElemental_WithoutAGuildElemental_DoesNothing()
    {
        var p = Party(members: 1);
        p.Members[0].Spells[41] = 1;
        p.Members[0].Mp = p.Members[0].MpMax = 30;
        var b = P4Battle.Dungeon(Manual, T, B, p, new Low(), 0);
        Alone(b);
        b.ChooseOption(P4EncounterOption.Fight);
        b.Order(new P4Order(P4ActionKind.Cast, 24, 42));
        Assert.DoesNotContain(b.Members, m => m.Elemental);
    }

    [OriginalDataFact]
    public void PartyElemental_IsSaved()
    {
        var p = Party(members: 1);
        p.Elemental = 2;
        Assert.Equal(2, P4PartySnapshot.From(p).ToParty().Elemental);
    }

    [OriginalDataFact]
    public void Teleportation_OnTheArrivalSquare_StaysThere()
    {
        // A fight from the load event (NOENC; FIGHT 1; END) before any step: OldX / OldY still hold another map's square.
        var words = new ushort[31 * 31];
        Array.Fill(words, (ushort)((31 << 6) | (7 << 11)));
        var file = new P4DungeonFile(1, "test", words, [new P4EventTrigger(0x20, 0, 4, 0)], [0x22, 0x23, 0x01, 0x00]);
        var party = Party(members: 2);
        party.Members[0].Spells[38] = 1;    // Teleportation (39)
        party.Members[0].Mp = party.Members[0].MpMax = 30;
        var c = new Phantasie4Campaign(Manual, T, Atlas, party, new Low(), _ => file, i => $"item{i}",
            new P4Location(P4Place.Outdoors, 1, 0, 0), B);
        c.EnterDungeon(1, 10, 10);
        c.Follow();
        var b = Assert.IsType<P4Battle>(c.Battle);
        Assert.True(c.Dungeon.NoStepSinceLoad);
        b.ChooseOption(P4EncounterOption.Fight);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, -1, 39)));
        while (b.Prompt == P4BattlePrompt.Order)
            b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(P4BattleEnd.Teleported, b.End);
        while (b.Prompt == P4BattlePrompt.Treasure)
            b.TakeItem(false);
        b.Continue();
        c.Follow();
        Assert.Equal((10, 10), (c.Dungeon.X, c.Dungeon.Y));
    }

    [OriginalDataFact]
    public void AutoSpells_TheCodeCastsMindBlast_TheManualSetDoesNot()
    {
        // E = 0 (always attack), Mind Blast 1 (34) only: rand = 1 passes the 75 % roll and the walk's (785 - 30 i) / 10 000.
        // D4-046: 34 is in the code's walk but not in the manual's p. 32 attack set.
        foreach (var (o, casts) in new[] { (Code, true), (Manual, false) })
        {
            var p = Party(members: 1);
            var m = p.Members[0];
            m.Strategy = [0, 0, 2, 6, 0];
            m.Spells[33] = 1;
            m.Mp = m.MpMax = 10;
            var b = P4Battle.Dungeon(o, T, B, p, new Low(), 0);
            Alone(b);
            b.ChooseOption(P4EncounterOption.Fight);
            var bm = b.Current!;
            b.AutoMove();
            Assert.Equal(casts, (bm.Order.Kind == P4ActionKind.Cast && bm.Order.Spell == 34) || m.Mp < 10);
        }
    }
}
