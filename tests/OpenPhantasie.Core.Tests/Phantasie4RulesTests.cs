using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's Core rules (T6.3; <c>reverse-engineering/x68000/phantasie4/logic-specs/</c>) and the decided D4 entries
/// as <c>p4.*</c> options. Tables come from <c>GameData/phantasie4/rule-tables.json</c>; expected numbers from the specs,
/// the manual and the user's s82 amounts.
/// </summary>
public class Phantasie4RulesTests
{
    /// <summary>Hands out the given values in order; each must lie in the asked range.</summary>
    private sealed class Script(params int[] values) : IRandomSource
    {
        private int _i;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.True(_i < values.Length, "script ran out");
            var v = values[_i++];
            Assert.InRange(v, minInclusive, maxExclusive - 1);
            return v;
        }

        public bool Done => _i == values.Length;
    }

    private static readonly Phantasie4RuleTables R = GameTables.Load(GameId.P4).Phantasie4!;
    private static P4Tables T => R.Tables;
    private static P4CreationTables C => T.Creation;
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4ManualAmounts None = new();

    // ---- options --------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Options_OneDescriptorPerDecidedEntry()
    {
        var p4 = RuleOptionRegistry.For(GameId.P4);
        Assert.Equal(P4RuleOptions.All.Count + 3, p4.Count); // + shared-aging, treasury, guild-limit
        Assert.Equal("guild-limit", p4[^1].Id);
        var entries = P4RuleOptions.All.Select(d => d.Difference).ToHashSet();
        foreach (var n in new[] { 1, 10, 12, 13, 15, 16, 18, 19, 20 }.Concat(Enumerable.Range(30, 27)))
            Assert.Contains($"D4-{n:000}", entries);
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P1), d => d.Id.StartsWith("p4.", StringComparison.Ordinal));
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P3), d => d.Id.StartsWith("p4.", StringComparison.Ordinal));
        // Manual and Remaster presets play the manual (or MSX2); the Original (X68000) preset the code (D4-031: uncapped).
        // Ship life and Teleportation are choices with the same value in every preset (user s84); so is the M overview
        // (D4-072, user s109: the code's blocks by default, the manual's centred view as the option), and the dungeon reload
        // (user s111, manual silent: the original's reload by default, the saved map as the option) and Annihilate Enemy's
        // effect (QoL, user s117: off = the code in every preset), and the T5 transfer options (user s72: off = the original's
        // Hall-only, III-only transfer in every preset).
        Assert.All(P4RuleOptions.All, d => Assert.Equal(d.Manual, d.Remaster));
        var sameEverywhere = new[]
        {
            P4RuleOptions.ShipLife, P4RuleOptions.Teleport, P4RuleOptions.Overview, P4RuleOptions.DungeonReload,
            P4RuleOptions.Annihilate, P4RuleOptions.TransferJoin, P4RuleOptions.TransferGames,
        };
        Assert.All(sameEverywhere, d => Assert.Equal(d.Manual, d.Dos));
        var twoSided = P4RuleOptions.All.Except(sameEverywhere).ToList();
        Assert.All(twoSided, d => Assert.Contains(d.Manual, new[] { "manual", "msx2" }));
        Assert.All(twoSided, d => Assert.Contains(d.Dos, new[] { "x68000", "uncapped" }));
        Assert.True(Code.P4GoldUncapped());
        Assert.False(Manual.P4GoldUncapped());
    }

    // ---- creation and ancestry (ancestry.md) ----------------------------------------------------------------------

    private static readonly P4Ancestor Imported = new(P4Ids.Human, P4Ids.Fighter, [15, 15, 15, 15, 15], HallSlot: 3);

    [OriginalDataFact]
    public void D4_001_NikademusAsAncestor()
    {
        var nikademus = R.Hall[2];
        Assert.Equal(2, nikademus.HallSlot);
        // Manual: allowed, counting as Human for the race combination.
        Assert.Equal(new P4SecondAncestorResult(false, P4Ids.Human, P4Ids.Human),
            P4Creation.SecondFromHall(Manual, C, nikademus, Imported));
        Assert.False(P4Creation.SecondFromHall(Manual, C, Imported, nikademus).NotPossible);
        var rng = new Script(30); // 30 ≤ 50: ancestor 2 is "the same race" (Human)
        Assert.Equal(new P4SecondAncestorResult(false, P4Ids.Human, P4Ids.Human),
            P4Creation.SecondBySameOrDifferent(Manual, C, rng, nikademus, same: true));
        // X68000: every path with him ends in "Not possible".
        Assert.True(P4Creation.SecondFromHall(Code, C, nikademus, Imported).NotPossible);
        Assert.True(P4Creation.SecondFromHall(Code, C, Imported, nikademus).NotPossible);
        // The code rolls rand(100) first, then refuses (_mopta tests __reg_fame after the dice).
        Assert.True(P4Creation.SecondBySameOrDifferent(Code, C, new Script(30), nikademus, same: true).NotPossible);
    }

    [OriginalDataFact]
    public void HallPairs_ShippedThreeAndSamePersonAreNotPossible()
    {
        Assert.True(P4Creation.SecondFromHall(Manual, C, R.Hall[0], R.Hall[1]).NotPossible); // Wood + Filmon
        Assert.True(P4Creation.SecondFromHall(Manual, C, Imported, Imported).NotPossible);
        var orc = new P4Ancestor(P4Ids.Orc, P4Ids.Fighter, [16, 16, 14, 12, 11], HallSlot: 4);
        Assert.Equal(30, P4Creation.SecondFromHall(Manual, C, R.Hall[0], orc).DescendantRace); // Wood + Orc = Half Orc
        var orc2 = orc with { HallSlot = 5 };
        Assert.True(P4Creation.SecondFromHall(Manual, C, orc, orc2).NotPossible); // Orc + Orc has no combination
    }

    [OriginalDataFact]
    public void SecondAncestor_DifferentRaceRoll()
    {
        var elf = new P4Ancestor(P4Ids.Elf, P4Ids.Wizard, [10, 16, 14, 10, 12]);
        // r = 80 > 25: one of the races combining with Elf (Human → Half Elf, Elf → Elf), rand(2) = 1 → Human.
        var rng = new Script(80, 1);
        // s152: _mopta draws both combining races, each with a _noise (RacesShown).
        Assert.Equal(new P4SecondAncestorResult(false, P4Ids.Human, 19) { RacesShown = 2 },
            P4Creation.SecondBySameOrDifferent(Manual, C, rng, elf, same: false));
        Assert.True(rng.Done);
        // r = 20 ≤ 25: the same race after all — "Sorry, the ancestor you found was …".
        Assert.Equal(new P4SecondAncestorResult(false, P4Ids.Elf, P4Ids.Elf, Sorry: true),
            P4Creation.SecondBySameOrDifferent(Manual, C, new Script(20), elf, same: false));
    }

    [OriginalDataFact]
    public void D4_010_ElvesAndDwarvesRollHigher()
    {
        Assert.Equal(3, P4Creation.RollSocial(Manual, C, new Script(85), P4Ids.Elf));   // 95: Noble
        Assert.Equal(2, P4Creation.RollSocial(Code, C, new Script(85), P4Ids.Elf));     // 85: Craftsman
        Assert.Equal(3, P4Creation.RollSocial(Manual, C, new Script(100), P4Ids.Dwarf)); // capped at 100
        Assert.Equal(2, P4Creation.RollSocial(Manual, C, new Script(85), P4Ids.Human));
        Assert.Equal(0, P4Creation.RollSocial(Manual, C, new Script(29), P4Ids.Human));
        Assert.Equal(1, P4Creation.RollSocial(Manual, C, new Script(30), P4Ids.Human));
        Assert.Equal(2, P4Creation.RollSocial(Manual, C, new Script(75), P4Ids.Orc));  // rand(75): never Noble
    }

    [OriginalDataFact]
    public void D4_012_Strides()
    {
        Assert.Equal(6, P4Creation.Stride(Manual, C, P4Ids.Halfling, P4Ids.Thief)); // 5 + 1
        Assert.Equal(7, P4Creation.Stride(Code, C, P4Ids.Halfling, P4Ids.Thief));   // 6 + 1
        Assert.Equal(8, P4Creation.Stride(Manual, C, P4Ids.Orc, P4Ids.Fighter));
        Assert.Equal(7, P4Creation.Stride(Code, C, P4Ids.Orc, P4Ids.Fighter));
        Assert.Equal(P4Creation.Stride(Manual, C, P4Ids.Human, P4Ids.Wizard), P4Creation.Stride(Code, C, P4Ids.Human, P4Ids.Wizard));
    }

    [OriginalDataFact]
    public void D4_013_GargoyleClasses()
    {
        Assert.True(P4Creation.ClassAllowed(Manual, C, P4Ids.GargoyleRace, P4Ids.Thief));
        Assert.True(P4Creation.ClassAllowed(Manual, C, P4Ids.GargoyleRace, P4Ids.Fighter));
        Assert.False(P4Creation.ClassAllowed(Code, C, P4Ids.GargoyleRace, P4Ids.Thief));
        Assert.True(P4Creation.ClassAllowed(Code, C, P4Ids.GargoyleRace, P4Ids.GargoyleClass));
        Assert.False(P4Creation.ClassAllowed(Manual, C, P4Ids.GargoyleRace, P4Ids.Priest));
        Assert.False(P4Creation.ClassAllowed(Manual, C, P4Ids.HillGiant, P4Ids.Thief));
        Assert.True(P4Creation.ClassAllowed(Manual, C, 19, P4Ids.ShadowWarrior)); // Half Elf only
        Assert.False(P4Creation.ClassAllowed(Manual, C, P4Ids.Human, P4Ids.ShadowWarrior));
    }

    [OriginalDataFact]
    public void RandomCreatureLists()
    {
        Assert.Equal([5, 6, 8, 10, 11, 12, 13, 14], P4Creation.RandomRaceList(C, new Script(26), false, false));
        Assert.Equal([7, 9, 15, 16, 17], P4Creation.RandomRaceList(C, new Script(25), false, false));
        Assert.Equal([7, 9, 17], P4Creation.RandomRaceList(C, new Script(25), false, true));
        Assert.Equal([7, 9], P4Creation.RandomRaceList(C, new Script(1), true, false));
    }

    [OriginalDataFact]
    public void Abilities_DiceInheritanceAndBonuses()
    {
        // Three dice of 6 × 1^k: 21, + Fighter Str 2 → capped 18, + Human 0.
        Assert.Equal(18, P4Creation.RollAbility(C, new Script(100, 100, 100), P4Ids.Human, P4Ids.Fighter, 0, 0.6));
        // u = 0.01: 6 × 0.01^0.6 ≈ 0.379 each → trunc(1.14) + 3 = 4, + 2 = 6.
        Assert.Equal(6, P4Creation.RollAbility(C, new Script(1, 1, 1), P4Ids.Human, P4Ids.Fighter, 0, 0.6));
        Assert.Equal(0.491, P4Creation.InheritanceExponent(Manual, C, 13.5));
        Assert.Equal(0.05, P4Creation.InheritanceExponent(Manual, C, 18.0));
        Assert.Equal(5.635, P4Creation.InheritanceExponent(Manual, C, 5.0));
        // Filmon (Sage) has no class row: _sub_bonus reads 6, 6, 7, 7, 8 (ancestry.md 5.2).
        Assert.Equal([6, 15, 7, 6, 7], Enumerable.Range(0, 5).Select(a => P4Creation.RawAbility(C, R.Hall[1], a)));
    }

    [OriginalDataFact]
    public void HallSocialDivineAidAndRecord()
    {
        var superHero = Imported with { Social = 4, GameResults = [0, 0, 100] };
        Assert.Equal(7, P4Creation.HallSocial(C, 1, [R.Hall[0], superHero]));  // Royalty
        Assert.Equal(1, P4Creation.HallSocial(C, 1, [R.Hall[0], R.Hall[1]]));  // Wood / Filmon pass nothing on
        Assert.Equal(1, P4Creation.HallSocial(C, 1, [new P4Ancestor(0, 3, [10, 10, 10, 10, 10], Social: 4)])); // a new person
        Assert.Equal(8, P4Creation.HallSocial(C, 1, [Imported with { Social = 5 }])); // Outcast
        Assert.True(P4Creation.KnowsDivineAid([R.Hall[0], superHero]));
        Assert.False(P4Creation.KnowsDivineAid([superHero, R.Hall[0]])); // the last Hall ancestor counts
        Assert.Equal(768, P4Creation.StartingGold(C, 3)); // manual p. 25
        Assert.Equal(2, P4Creation.Origin(1)); // Scandor
        var kit = P4Creation.StartingKit(C, P4Ids.Priest);
        Assert.Equal([1, 46], kit.Spells);
        Assert.Equal(79, kit.Item);
        Assert.Null(P4Creation.StartingKit(C, P4Ids.ShadowWarrior).Item);
        Assert.Equal(99, P4Creation.NextDescendantCount(C, 99));
        Assert.InRange(P4Creation.RollLuck(new Script(1)), 9, 9);
    }

    [OriginalDataFact]
    public void NewRecord_ReproducesARecoveredTestSaveCharacter()
    {
        // deleted-disk2/_uils.1 slot 3 (tools/scripts/x68k_p4_saves.py): a Halfling Thief, abilities 5/17/18/13/17, Luck 9,
        // Peasant, HP 8, magic level 6, MP 4, stride 7, aging factor 3 (slot 0 is a Minotaur: the code's slot-0 race bug),
        // age units 338, skills as below. Script: social 10, HP dice 3 + 3 + 2, Luck rand(6) = 1, age rand(100) = 8.
        var rng = new Script(10, 3, 3, 2, 1, 8);
        var c = P4CreationRecord.Create(Code, T, rng, "Thief", P4Ids.Halfling, P4Ids.Thief, [5, 17, 18, 13, 17], [], 1,
            agingRace: 14);
        Assert.True(rng.Done);
        Assert.Equal([35, 41, 33, 44, 46, 30, 47, 42, 16, 21, 25], c.Skills);
        Assert.Equal((0, 8, 6, 4, 7, 3, 338), (c.Social, c.HpMax, c.MagicLevel, c.MpMax, c.Stride, c.AgingFactor, c.AgeUnits));
        Assert.Equal([5, 17, 18, 13, 17, 9], c.Abilities);
        Assert.Equal((0L, 1, (int?)79, 2), (c.Account, c.Level, c.Item, c.Origin));
        Assert.Empty(c.KnownSpells);
        // Under the manual the Halfling strides 5 (+1 Thief), ages by its own race (9 × 2.5) and a Thief's level-1 HP
        // value is 1 (D4-019), so one HP die.
        var m = P4CreationRecord.Create(Manual, T, new Script(10, 3, 1, 8), "Thief", P4Ids.Halfling, P4Ids.Thief,
            [5, 17, 18, 13, 17], [], 1);
        Assert.Equal((6, 22, 3), (m.Stride, m.AgingFactor, m.HpMax));
        // A descendant of a Hall person who completed III knows Divine Aid (spell 63) and names the slot to bump.
        var hero = Imported with { GameResults = [0, 0, 100] };
        var d = P4CreationRecord.Create(Manual, T, new Script(10, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1), "Heir", P4Ids.Human,
            P4Ids.Fighter, [15, 10, 15, 15, 10], [hero, R.Hall[0]], 1);
        Assert.DoesNotContain(63, d.KnownSpells); // the last Hall ancestor (Wood) completed nothing
        var e = P4CreationRecord.Create(Manual, T, new Script(10, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1), "Heir", P4Ids.Human,
            P4Ids.Fighter, [15, 10, 15, 15, 10], [R.Hall[0], hero], 1);
        Assert.Contains(63, e.KnownSpells);
        Assert.Equal([0, 3], e.HallAncestorSlots);
        Assert.Throws<ArgumentException>(() =>
            P4CreationRecord.Create(Manual, T, new Script(), "X", P4Ids.HillGiant, P4Ids.Thief, [10, 10, 10, 10, 10], [], 1));
    }

    // ---- aging (D4-033 / D4-034) ----------------------------------------------------------------------------------

    [OriginalDataFact]
    public void AgingFactorAndStartingAge()
    {
        var a = T.Aging;
        Assert.Equal(190, P4Aging.AgingFactor(a, P4Ids.Elf, 10));
        Assert.Equal(255, P4Aging.AgingFactor(a, P4Ids.Elf, 14));
        Assert.Equal(10, P4Aging.AgingFactor(a, P4Ids.Human, 10));
        Assert.Equal(22, P4Aging.AgingFactor(a, P4Ids.Dwarf, 9));
        Assert.Equal(4, P4Aging.AgingFactor(a, P4Ids.Orc, 10));
        Assert.Equal(516, P4Aging.StartingAge(new Script(1), 10)); // 260 + 256 × 10 × 0.1
        Assert.Equal(2, P4Aging.AgeUnitsFromDays(a, 0x700 * 2 + 5));
    }

    [OriginalDataFact]
    public void D4_033_Lifespans()
    {
        var a = T.Aging;
        Assert.Equal(1800, P4Aging.ManualLifespanYears(a, P4Ids.Elf, 9), 6);
        Assert.Equal(88, P4Aging.ManualLifespanYears(a, P4Ids.Human, 14), 6);
        Assert.Equal(45.9, P4Aging.ManualLifespanYears(a, P4Ids.Halfling, 12), 6);
        Assert.Equal(220, P4Aging.ManualLifespanYears(a, P4Ids.Gnome, 20), 6); // Luck clamped to 14
        // Human, Luck 9: 72 years; grown older from 50.4.
        Assert.Equal(P4AgeEvent.DiedOfOldAge, P4Aging.Check(Manual, a, P4Ids.Human, 9, 0, 72 * 26, false));
        Assert.Equal(P4AgeEvent.GrownOlder, P4Aging.Check(Manual, a, P4Ids.Human, 9, 0, 51 * 26, false));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(Manual, a, P4Ids.Human, 9, 0, 51 * 26, true));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(Manual, a, P4Ids.Human, 9, 0, 40 * 26, false));
        // X68000: A = age >> 8 against F = 10.
        Assert.Equal(P4AgeEvent.DiedOfOldAge, P4Aging.Check(Code, a, P4Ids.Human, 10, 10, 2560, false));
        Assert.Equal(P4AgeEvent.GrownOlder, P4Aging.Check(Code, a, P4Ids.Human, 10, 10, 1792, false));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(Code, a, P4Ids.Human, 10, 10, 1791, false));
        Assert.Equal(P4AgeEvent.None, P4Aging.Check(Code, a, P4Ids.Elf, 10, 190, 40000, false)); // signed: never
    }

    [OriginalDataFact]
    public void D4_034_GrowingOlder()
    {
        Assert.Equal(14, P4Aging.ScaleOnAging(Manual, 18));
        Assert.Equal(0, P4Aging.ScaleOnAging(Code, 18));
    }

    // ---- training (training.md, skills.md) ------------------------------------------------------------------------

    [OriginalDataFact]
    public void Training_ExperienceAndCost()
    {
        var t = T.Training;
        int[] fighter = [17, 10, 18, 10, 10, 10]; // r = (2 × 17 + 18) / 33
        Assert.Equal(2090, P4Training.ExperienceNeeded(t, P4Ids.Fighter, fighter, 2));
        Assert.Equal(171 - 3333, P4Training.ExperienceNeeded(t, P4Ids.Fighter, fighter, 0));
        Assert.True(P4Training.ExperienceNeeded(t, P4Ids.Sage, fighter, 1) > 100_000_000);
        // training.md 3 examples at T = 100.
        Assert.Equal(2207, P4Training.TrainingCost(t, 5, 12, 100));
        Assert.Equal(10143, P4Training.TrainingCost(t, 10, 12, 100));
        Assert.Equal(25000, P4Training.TrainingCost(t, 10, 3, 100));
        Assert.Equal(0, P4Training.TrainingCost(t, 10, 20, 100));
    }

    [OriginalDataFact]
    public void D4_015_PriestLevelsAndSpells()
    {
        var t = T.Training;
        Assert.Equal(35, P4Training.MaxLevel(Manual, t, P4Ids.Priest));
        Assert.Equal(30, P4Training.MaxLevel(Code, t, P4Ids.Priest));
        Assert.Equal(30, P4Training.MaxLevel(Manual, t, P4Ids.Wizard));
        Assert.Equal(P4TrainingGate.Allowed, P4Training.Gate(Manual, t, P4Ids.Priest, 31, 0, true));
        Assert.Equal(P4TrainingGate.NothingMoreToTeach, P4Training.Gate(Code, t, P4Ids.Priest, 31, 0, true));
        Assert.Equal(P4TrainingGate.NothingMoreToTeach, P4Training.Gate(Manual, t, P4Ids.Fighter, 10, 125, true));
        Assert.Equal(P4TrainingGate.PoorCondition, P4Training.Gate(Manual, t, P4Ids.Fighter, 10, 0, false));
        Assert.True(P4Training.CanLevelUp(Manual, t, P4Ids.Priest, 34));
        Assert.False(P4Training.CanLevelUp(Code, t, P4Ids.Priest, 30));
        // The schedule (35 spells) plus 6 at each of 31–35 = all 65.
        // The schedule (rows 0-14, 35 spells) plus 6 at each of 31-35 = all 65. Levels 15-29 re-read the schedule from row 1
        // (_calpow's row wraps, training.md 4.1), which adds nothing new for a Priest.
        var all = Enumerable.Range(0, 15).Concat(Enumerable.Range(30, 5))
            .SelectMany(l => P4Training.SpellsGranted(Manual, t, P4Ids.Priest, l)).ToList();
        Assert.Equal(65, all.Count);
        Assert.Equal(Enumerable.Range(1, 65), all.Order());
        Assert.Equal(65, Enumerable.Range(0, 35).SelectMany(l => P4Training.SpellsGranted(Manual, t, P4Ids.Priest, l))
            .Distinct().Count());
        Assert.Equal(6, P4Training.SpellsGranted(Manual, t, P4Ids.Priest, 30).Count);
        Assert.Equal(35, Enumerable.Range(0, 15).Sum(l => P4Training.SpellsGranted(Code, t, P4Ids.Priest, l).Count));
        Assert.Equal(35, Enumerable.Range(0, 29).SelectMany(l => P4Training.SpellsGranted(Code, t, P4Ids.Priest, l))
            .Distinct().Count());
        // L = 29: row index -1, the previous class's (Fighter's) level-15 row [C].
        Assert.Equal(t.SpellsGranted[P4Ids.Fighter][14], P4Training.SpellsGranted(Code, t, P4Ids.Priest, 29));
    }

    [OriginalDataFact]
    public void D4_016_FighterSpells()
    {
        var t = T.Training;
        var manual = Enumerable.Range(0, 15).SelectMany(l => P4Training.SpellsGranted(Manual, t, P4Ids.Fighter, l)).ToList();
        var code = Enumerable.Range(0, 15).SelectMany(l => P4Training.SpellsGranted(Code, t, P4Ids.Fighter, l)).ToList();
        Assert.Equal(4, manual.Count);
        Assert.Equal(7, code.Count);
        Assert.Equal(code.Take(4), manual);
        // A Thief's four are untouched.
        Assert.Equal(4, Enumerable.Range(0, 15).Sum(l => P4Training.SpellsGranted(Manual, t, P4Ids.Thief, l).Count));
    }

    [OriginalDataFact]
    public void D4_019_ThiefHp()
    {
        var manual = P4Training.HpGrowthRow(Manual, T.Training, P4Ids.Thief);
        var code = P4Training.HpGrowthRow(Code, T.Training, P4Ids.Thief);
        Assert.Equal((8, 24, 39), (manual[4], manual[9], manual[14])); // user s82: L5 8, L10 24, L15 39
        Assert.Equal((16, 35, 55), (code[4], code[9], code[14]));
        Assert.Equal(1, manual[0]); // Shadow Warrior's 2 − 1
        // New max HP: n = (value × (Con + 5)) >> 4 rolls of rand(3).
        Assert.Equal(9, P4Training.NewMaxHp(Code, T.Training, new Script(3, 3, 3), P4Ids.Thief, 0, 11, 0));
        Assert.Equal(2, P4Training.NewMaxHp(Manual, T.Training, new Script(2), P4Ids.Thief, 0, 11, 0));
        Assert.Equal(6, P4Training.NewMaxHp(Manual, T.Training, new Script(2), P4Ids.Thief, 0, 11, 5)); // old + 1
    }

    [OriginalDataFact]
    public void LevelUpGold_AndD4_031()
    {
        Assert.Equal(4050, P4Training.LevelUpGold(Manual, T.Training, T.Towns, 3, 5, 0)); // Noble: 27 × 5 × 30
        Assert.Equal(0, P4Training.LevelUpGold(Manual, T.Training, T.Towns, 0, 5, 0));
        Assert.Equal(65000, P4Training.LevelUpGold(Manual, T.Training, T.Towns, 7, 1, 0)); // Royalty: capped
        Assert.Equal(823543L * 30, P4Training.LevelUpGold(Code, T.Training, T.Towns, 7, 1, 0)); // uncapped
    }

    [OriginalDataFact]
    public void Skills_BonusAndValue()
    {
        int[] elf = [10, 12, 18, 10, 10, 10];
        Assert.Equal(47, P4Training.SkillBonus(P4Skill.FireBow, elf, P4Ids.Elf)); // the manual's Elf +15
        Assert.Equal(32, P4Training.SkillBonus(P4Skill.FireBow, elf, P4Ids.Human));
        Assert.Equal(24, P4Training.SkillBonus(P4Skill.Throwing, elf, P4Ids.Elf));
        Assert.Equal(0, P4Training.SkillBonus(P4Skill.Attack, [9, 0, 0, 0, 0, 0], 0));
        Assert.Equal(16, P4Training.SkillBonus(P4Skill.Attack, [18, 0, 0, 0, 0, 0], 0));
        Assert.Equal(51, P4Training.SkillValue(T.Training, P4Ids.Thief, P4Skill.Attack, 0, 0, 16)); // 70 / 2 + 16
    }

    [OriginalDataFact]
    public void MagicHall()
    {
        var t = T.Training;
        Assert.Equal(30, P4Training.SpellPrice(t, P4Ids.Wizard, 3, 18, 12, 100));
        Assert.True(P4Training.MayLearnMore(t, P4Ids.Wizard, 1, 18, 1, 1));
        Assert.False(P4Training.MayLearnMore(t, P4Ids.Wizard, 1, 18, 3, 1));
        Assert.False(P4Training.MayLearnMore(t, P4Ids.Wizard, 1, 18, 0, 0));
        Assert.True(P4Training.LessonSucceeds(new Script(89), 18));
        Assert.False(P4Training.LessonSucceeds(new Script(90), 18));
    }

    // ---- towns (towns.md) ------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void D4_030_TownEntryAndShares()
    {
        Assert.True(P4Town.DepositsOnEntry(Manual));
        Assert.False(P4Town.DepositsOnEntry(Code));
        Assert.Equal([33L, 66, 0], P4Town.ShareOut(100, [1, 2, 0]));
        Assert.Equal([49L, 9_999_999], P4Town.ShareExperience(10, [0, 9_999_990], [1, 1])); // 100 / 2.0001 → 49 each
    }

    [OriginalDataFact]
    public void D4_031_GoldCapsOnEveryPath()
    {
        var t = T.Towns;
        Assert.Equal(65000, P4Town.Cap(Manual, t, 70000, P4Town.GoldPath.Loot));
        Assert.Equal(70000, P4Town.Cap(Code, t, 70000, P4Town.GoldPath.Loot));
        Assert.Equal([65000L, 49], P4Town.Deposit(Manual, t, 100, [64990, 0], [1, 1]));
        Assert.Equal([65039L, 49], P4Town.Deposit(Code, t, 100, [64990, 0], [1, 1]));
        Assert.Equal((500L, 65000L), P4Town.Withdraw(Manual, t, 1000, 64500, 1000));
        Assert.Equal((0L, 65500L), P4Town.Withdraw(Code, t, 1000, 64500, 1000));
        Assert.Equal((1000L, 66000L), P4Town.Withdraw(Manual, t, 1000, 66000, 500)); // no wrap below 0
    }

    [OriginalDataFact]
    public void D4_032_InnTime() =>
        Assert.Equal((14, 0), (P4Town.InnStayDays(Manual, T.Towns), P4Town.InnStayDays(Code, T.Towns)));

    [OriginalDataFact]
    public void D4_020_ShopAndResale()
    {
        Assert.Equal(80, P4Town.BuyPrice(100, 80));
        Assert.Equal(26, P4Town.SellPrice(100));
        Assert.Equal(32, P4Town.ResalePrice(Manual, T.Towns, 100, 80)); // user s82: pays 26, resells at 32
        Assert.Equal(80, P4Town.ResalePrice(Code, T.Towns, 100, 80));
        Assert.Equal(Enumerable.Repeat(96, 8), P4Town.RandomShopItems(T.Towns, new Script(1, 1, 1, 1, 1, 1, 1, 1), 0)); // Magic 2
    }

    [OriginalDataFact]
    public void D4_018_LongSword()
    {
        Assert.Equal(14, P4Town.ItemPoints(Manual, T.Towns, 40, 13));
        Assert.Equal(13, P4Town.ItemPoints(Code, T.Towns, 40, 13));
        Assert.Equal(12, P4Town.ItemPoints(Manual, T.Towns, 39, 12));
    }

    // ---- wounds (combat.md 5-6, items.md 3) -------------------------------------------------------------------------

    [Fact]
    public void HealingPoints_WorstLevelFirst()
    {
        int[] w = [3, 1, 2, 0, 1, 0];
        P4Body.Heal(w, 3);
        Assert.Equal([0, 1, 2, 0, 1, 0], w);
        P4Body.Heal(w, 3); // serious (2) + one light (1)
        Assert.Equal([0, 0, 0, 0, 1, 0], w);
        int[] s = [0, 0, 2, 0, 0, 0];
        P4Body.Heal(s, 1);
        Assert.Equal([0, 0, 2, 0, 0, 0], s); // a serious wound needs 2
    }

    [OriginalDataFact]
    public void D4_035_HealingPotions()
    {
        Assert.Equal((1, 1), P4Body.HealingPotion(Manual, T.Towns, 1));
        Assert.Equal((1, 0), P4Body.HealingPotion(Code, T.Towns, 1));
        Assert.Equal((16, 2), P4Body.HealingPotion(Manual, T.Towns, 4));
        Assert.Equal((16, 1), P4Body.HealingPotion(Code, T.Towns, 4));
        Assert.Equal((35, 2), P4Body.HealingPotion(Manual, T.Towns, 6));
        Assert.Equal((36, 2), P4Body.HealingPotion(Code, T.Towns, 6));
        Assert.Equal((100, 4), P4Body.HealingPotion(Manual, T.Towns, 10));
        Assert.Equal((100, 3), P4Body.HealingPotion(Code, T.Towns, 10));
    }

    [OriginalDataFact]
    public void D4_043_HeadOrTorsoLost()
    {
        int[] lost = [0, 3, 0, 0, 0, 0];
        Assert.True(P4Body.WoundsKill(Manual, lost));
        Assert.False(P4Body.WoundsKill(Code, lost));
        Assert.False(P4Body.WoundsKill(Manual, [2, 2, 3, 3, 3, 3]));
    }

    [Fact]
    public void HitLocationAndWoundLevel()
    {
        Assert.Equal(P4BodyPart.LeftArm, P4Body.HitLocation(new Script(1), 5, true));
        Assert.Equal(P4BodyPart.LeftLeg, P4Body.HitLocation(new Script(3), 5, true));
        Assert.Equal(P4BodyPart.Head, P4Body.HitLocation(new Script(50), 10, true));  // 0.5^5 × 6 = 0.19
        Assert.Equal(P4BodyPart.LeftArm, P4Body.HitLocation(new Script(100), 1, true)); // part 6 → 3
        Assert.Equal(3, P4Body.WoundLevel(new Script(100), 10, 10, 0));
        Assert.Equal(1, P4Body.WoundLevel(new Script(10), 10, 10, 0));
        Assert.Equal(2, P4Body.WoundLevel(new Script(10), 10, 10, 2)); // never lowers
    }

    // ---- combat (combat.md) -----------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void D4_044_MpCosts()
    {
        var c = T.Combat;
        Assert.Equal((1, 2), (P4Combat.MpCost(Manual, c, 11), P4Combat.MpCost(Code, c, 11)));
        Assert.Equal((2, 3), (P4Combat.MpCost(Manual, c, 12), P4Combat.MpCost(Code, c, 12)));
        Assert.Equal((3, 2), (P4Combat.MpCost(Manual, c, 25), P4Combat.MpCost(Code, c, 25)));
        Assert.Equal((2, 3), (P4Combat.MpCost(Manual, c, 26), P4Combat.MpCost(Code, c, 26)));
        Assert.Equal((1, 3), (P4Combat.MpCost(Manual, c, 48), P4Combat.MpCost(Code, c, 48)));
        Assert.Equal((4, 4), (P4Combat.MpCost(Manual, c, 49), P4Combat.MpCost(Code, c, 49)));
    }

    [OriginalDataTheory]
    [InlineData(1, 5, 5)]
    [InlineData(2, 6, 15)]
    [InlineData(3, 7, 18)]
    [InlineData(10, 14, 18)]
    public void D4_055_MonsterHp(int roll, int msx2, int x68000)
    {
        Assert.Equal(msx2, P4Combat.MonsterHp(Manual, T.Combat, new Script(roll), 10));
        Assert.Equal(x68000, P4Combat.MonsterHp(Code, T.Combat, new Script(roll), 10));
    }

    [OriginalDataFact]
    public void D4_042_Strikes()
    {
        var c = T.Combat;
        Assert.Equal(2, P4Combat.Strikes(Manual, c, P4WeaponAction.Attack, 100));
        Assert.Equal(1, P4Combat.Strikes(Code, c, P4WeaponAction.Attack, 100));
        Assert.Equal(3, P4Combat.Strikes(Manual, c, P4WeaponAction.Slash, 150));
        Assert.Equal(4, P4Combat.Strikes(Manual, c, P4WeaponAction.Slash, 225));
        Assert.Equal(1, P4Combat.Strikes(Code, c, P4WeaponAction.Slash, 225));
        Assert.Equal(1, P4Combat.Strikes(Manual, c, P4WeaponAction.Thrust, 225));
    }

    [OriginalDataFact]
    public void WeaponHitAndDamage()
    {
        var c = T.Combat;
        Assert.Equal(40, P4Combat.MeleeHitChance(Code, c, P4WeaponAction.Slash, 150, 10));
        Assert.Equal(0, P4Combat.MeleeHitChance(Code, c, P4WeaponAction.AimBlow, 100, 90));
        Assert.Equal(200, P4Combat.MeleeHitChance(Code, c, P4WeaponAction.Attack, 100, 90, targetHelpless: true));
        Assert.Equal(75, P4Combat.MeleeHitChance(Code, c, P4WeaponAction.Attack, 100, -5, attackerDown: true));
        Assert.Equal(75, P4Combat.MissileHitChance(80, 20));
        Assert.Equal(80, P4Combat.MissileHitChance(80, -5));
        Assert.Equal(9, P4Combat.MeleeDamage(Code, new Script(100), P4WeaponAction.Thrust, 10, 2, 100));
        Assert.Equal(5, P4Combat.MeleeDamage(Code, new Script(100), P4WeaponAction.Slash, 10, 2, 150));
        Assert.Equal(14, P4Combat.MissileDamage(new Script(100), 10, 4, 4));
        Assert.Equal(1, P4Combat.MissileDamage(new Script(100), 0, 0, 0)); // 0 → 1 (quirk)
        Assert.Equal(0, P4Combat.MissileDamage(new Script(100), 0, 0, -4)); // < 0 → 0
    }

    [OriginalDataFact]
    public void D4_037_Throwing()
    {
        Assert.Equal(40, P4Combat.MissileSkill(Manual, fireBow: 80, throwing: 40, isThrow: true));
        Assert.Equal(80, P4Combat.MissileSkill(Code, fireBow: 80, throwing: 40, isThrow: true));
        Assert.Equal(80, P4Combat.MissileSkill(Manual, fireBow: 80, throwing: 40, isThrow: false));
    }

    [OriginalDataFact]
    public void D4_045_PartyArrowFlame()
    {
        var c = T.Combat;
        Assert.Equal((50, 15), P4Combat.ArrowFlame(Manual, c, 26));
        Assert.Equal((0, 15), P4Combat.ArrowFlame(Code, c, 26));
        Assert.Equal((100, 25), P4Combat.ArrowFlame(Manual, c, 27));
        Assert.Equal((50, 15), P4Combat.ArrowFlame(Code, c, 24));
    }

    [OriginalDataFact]
    public void D4_050_Weakness()
    {
        Assert.Equal((1.0, 0.6), P4Combat.Weakness(Manual, T.Combat, 2));
        Assert.Equal((0.6, 1.0), P4Combat.Weakness(Code, T.Combat, 2));
    }

    [OriginalDataFact]
    public void D4_051_SpellDamage()
    {
        var c = T.Combat;
        Assert.Equal(10, P4Combat.FireFlash(Manual, c, new Script(10), 1, 18));
        Assert.Equal(12, P4Combat.FireFlash(Code, c, new Script(10), 1, 18));
        Assert.Equal(8, P4Combat.MindBlast(Manual, c, new Script(8), 1, 77));
        Assert.Null(P4Combat.MindBlast(Code, c, new Script(), 1, 77)); // 77 = 7 × 11 < 2000
        Assert.Equal(7, P4Combat.MindBlast(Code, c, new Script(8), 1, 78));
        Assert.Equal(1, P4Combat.MindBlast(Code, c, new Script(1), 1, 78));
        Assert.Equal(9, P4Combat.HealingSpell(Manual, c, new Script(9), 1));
        Assert.Equal(0, P4Combat.HealingSpell(Code, c, new Script(1), 1));
    }

    [OriginalDataFact]
    public void D4_047_Flameball()
    {
        var c = T.Combat;
        Assert.Equal([5, 13, 21, 21], Enumerable.Range(1, 4).Select(r => P4Combat.FlameballSquares(Code, c, r).Count));
        Assert.Equal([4, 12, 20, 28], Enumerable.Range(1, 4).Select(r => P4Combat.FlameballSquares(Manual, c, r).Count));
        Assert.DoesNotContain((0, 0), P4Combat.FlameballSquares(Manual, c, 4));
        Assert.Throws<P4AmountUnknownException>(() => P4Combat.FlameballDamage(Manual, c, None, new Script(18), 1, 2, 2));
        var a = T.Amounts;
        Assert.Null(P4Combat.FlameballDamage(Manual, c, a, new Script(), 1, 12, 10));      // Plate Mail: spared
        Assert.Equal(16, P4Combat.FlameballDamage(Manual, c, a, new Script(18), 1, 2, 9)); // 9-point body armour: burnt
        Assert.Equal(18, P4Combat.FlameballDamage(Manual, c, a, new Script(18), 1, 0, 0)); // a monster
        Assert.Equal(1, P4Combat.FlameballDamage(Code, c, None, new Script(5), 1, 30, 10));
    }

    [OriginalDataFact]
    public void D4_052_SpellWording()
    {
        Assert.Equal(0.5, P4Combat.SlowStrideFactor(Manual, 1));
        Assert.Equal(0.75, P4Combat.SlowStrideFactor(Manual, 2));
        Assert.Equal(0.25, P4Combat.SlowStrideFactor(Code, 2));
        Assert.Equal(1, P4Combat.DissolveRounds(Code, None));
        Assert.Throws<P4AmountUnknownException>(() => P4Combat.DissolveRounds(Manual, None));
        Assert.Equal(3, P4Combat.DissolveRounds(Manual, T.Amounts));
        Assert.Equal([10, 10, 10], P4Combat.DissolveDamageByRound(Manual, T.Amounts, 30));
        Assert.Equal([11, 10, 10], P4Combat.DissolveDamageByRound(Manual, T.Amounts, 31));
        Assert.Equal([32], P4Combat.DissolveDamageByRound(Code, T.Amounts, 32));
        Assert.Equal(P4FearOutcome.MonstersFlee, P4Combat.Fear(Manual, new Script(50), 5, 5));
        Assert.Equal(P4FearOutcome.LevelLowered, P4Combat.Fear(Code, new Script(50), 5, 5));
        Assert.Equal(P4FearOutcome.NoEffect, P4Combat.Fear(Manual, new Script(51), 5, 5));
        Assert.Equal(P4TeleportDestination.PreviousSquare, P4Combat.Teleport(Manual)); // user s84 / s103: previous square by default
        Assert.Equal(P4TeleportDestination.PreviousSquare, P4Combat.Teleport(Code));
        Assert.Equal(P4TeleportDestination.RandomSquare, P4Combat.Teleport(Manual.With("p4.teleport", "random")));
        Assert.Equal(P4TeleportDestination.Entrance, P4Combat.Teleport(Manual.With("p4.teleport", "entrance")));
        Assert.Equal(P4TeleportDestination.Stay, P4Combat.Teleport(Manual.With("p4.teleport", "stay"))); // D4-070
    }

    [OriginalDataFact]
    public void D4_046_AutoSpells()
    {
        var c = T.Combat;
        Assert.True(P4Combat.AutoMayCast(Manual, c, 38));   // Sleep: manual yes, code never
        Assert.False(P4Combat.AutoMayCast(Code, c, 38));
        Assert.False(P4Combat.AutoMayCast(Manual, c, 21));  // Confusion 2: code only
        Assert.True(P4Combat.AutoMayCast(Code, c, 21));
        Assert.False(P4Combat.AutoMayCast(Manual, c, 15));
        Assert.False(P4Combat.AutoMayCast(Code, c, 15));
        Assert.Equal([8, 7, 6, 5, 12, 62, 61, 60, 20, 59, 11, 14, 38, 43, 48], P4Combat.AutoAttackWalk(Manual, c));
        Assert.Equal(c.AutoAttackWalkCode, P4Combat.AutoAttackWalk(Code, c));
    }

    [OriginalDataFact]
    public void D4_048_NightWatch()
    {
        Assert.True(P4Combat.WatchWakesParty(Manual, true));
        Assert.False(P4Combat.WatchWakesParty(Code, true));
        Assert.False(P4Combat.WatchWakesParty(Manual, false));
    }

    [OriginalDataFact]
    public void D4_049_BegForMercy()
    {
        Assert.Equal((232L, 0), P4Combat.BegForMercy(Code, None, 1000));
        Assert.Throws<P4AmountUnknownException>(() => P4Combat.BegForMercy(Manual, None, 1000));
        Assert.Equal((0L, 1), P4Combat.BegForMercy(Manual, T.Amounts, 1000));
        // One random unequipped item from each member (user s84); a member with none gives none.
        var taken = P4Combat.BegItemsTaken(Manual, T.Amounts, new Script(2, 1), [[4, 7, 9], [], [3]]);
        Assert.Equal([[7], [], [3]], taken);
        Assert.All(P4Combat.BegItemsTaken(Code, T.Amounts, new Script(), [[4, 7, 9]]), Assert.Empty);
    }

    [OriginalDataFact]
    public void D4_036_SpotItemAndTreasure()
    {
        var c = T.Combat;
        Assert.True(P4Combat.TreasureFound(Code, c, new Script(45), false, true, false, 3, [100]));
        Assert.False(P4Combat.TreasureFound(Code, c, new Script(46), false, true, false, 3, [100]));
        Assert.True(P4Combat.TreasureFound(Code, c, new Script(28), true, true, false, 10, []));
        var none = new Script();
        Assert.True(P4Combat.TreasureFound(Manual, c, none, true, true, false, 3, [0]));   // outdoors, wiped out
        Assert.True(P4Combat.TreasureFound(Manual, c, none, false, false, true, 3, [0]));  // dungeon, surrendered
        Assert.True(none.Done);
        Assert.True(P4Combat.TreasureFound(Manual, c, new Script(50, 10), false, true, false, 3, [30, 10]));
        Assert.False(P4Combat.TreasureFound(Manual, c, new Script(50, 11), false, true, false, 3, [30, 10]));
        Assert.False(P4Combat.SpotIdentifies(Code, None, 200));
        Assert.Throws<P4AmountUnknownException>(() => P4Combat.SpotIdentifies(Manual, None, 200));
        Assert.True(P4Combat.SpotIdentifies(Manual, T.Amounts, 50));
        Assert.False(P4Combat.SpotIdentifies(Manual, T.Amounts, 49));
    }

    // ---- outdoors and non-combat spells ------------------------------------------------------------------------------

    [OriginalDataFact]
    public void D4_039_Levitation()
    {
        Assert.Equal(25, P4Outdoor.LevitationSteps(Manual, T.Outdoor, [8, 5, 6]));
        Assert.Null(P4Outdoor.LevitationSteps(Code, T.Outdoor, [8, 5, 6]));
        Assert.True(P4Outdoor.LevitationWorksOutdoors(Manual));
        Assert.False(P4Outdoor.LevitationWorksOutdoors(Code));
    }

    [OriginalDataFact]
    public void D4_040_Navigation()
    {
        Assert.Equal(1, P4Outdoor.Navigator([30, 45, 45])); // the best steers under both rules (user s84)
        Assert.Equal(1, P4Outdoor.Navigator([60, 70]));
        Assert.Null(P4Outdoor.Navigator([]));
        var o = T.Outdoor;
        Assert.True(P4Outdoor.Drifts(Code, o, new Script(31), 30));          // the code's roll
        Assert.False(P4Outdoor.Drifts(Code, o, new Script(30), 30));
        Assert.True(P4Outdoor.Drifts(Manual, o, new Script(30, 20), 30));    // below 50: 50 − 30 = 20 %
        Assert.False(P4Outdoor.Drifts(Manual, o, new Script(30, 21), 30));
        Assert.False(P4Outdoor.Drifts(Manual, o, new Script(50), 50));       // no second roll from 50
    }

    [OriginalDataFact]
    public void D4_038_ShipsAndD4_041_Examine()
    {
        Assert.False(P4Outdoor.ShipWreckedInFight(Code, None, new Script(), partyWon: false));
        Assert.Throws<P4AmountUnknownException>(() => P4Outdoor.ShipWreckedInFight(Manual, None, new Script(1), partyWon: false));
        Assert.True(P4Outdoor.ShipWreckedInFight(Manual, T.Amounts, new Script(5), partyWon: false));
        Assert.False(P4Outdoor.ShipWreckedInFight(Manual, T.Amounts, new Script(6), partyWon: false));
        Assert.False(P4Outdoor.ShipWreckedInFight(Manual, T.Amounts, new Script(), partyWon: true)); // a win never wrecks
        Assert.False(P4Outdoor.ShipWornOut(Code, 100_000));
        Assert.False(P4Outdoor.ShipWornOut(Manual, 3999));
        Assert.True(P4Outdoor.ShipWornOut(Manual, 4000));
        Assert.True(P4Outdoor.ShipWornOut(Manual.With("p4.ship-life", "2000"), 2000));
        Assert.False(P4Outdoor.ShipWornOut(Manual.With("p4.ship-life", "8000"), 4000));
        Assert.Equal(0, P4Outdoor.ExamineSearchBonus(Code, None));
        Assert.True(P4Outdoor.ExamineWalks(Code));
        Assert.Throws<P4AmountUnknownException>(() => P4Outdoor.ExamineSearchBonus(Manual, None));
        Assert.Equal(20, P4Outdoor.ExamineSearchBonus(Manual, T.Amounts));
    }

    [OriginalDataFact]
    public void D4_053_IdentifyAndD4_054_MonsterEvaluation()
    {
        var a = T.Amounts;
        Assert.True(P4Outdoor.IdentifySucceeds(Code, None, new Script(), 1));
        Assert.Throws<P4AmountUnknownException>(() => P4Outdoor.IdentifySucceeds(Manual, None, new Script(1), 1));
        Assert.True(P4Outdoor.IdentifySucceeds(Manual, a, new Script(70), 4));   // 50 + 5 × 4 = 70 %
        Assert.False(P4Outdoor.IdentifySucceeds(Manual, a, new Script(71), 4));
        Assert.True(P4Outdoor.IdentifySucceeds(Manual, a, new Script(100), 10)); // level 10: always
        Assert.Equal(5, P4Outdoor.MonsterEvaluation(Code, None, new Script(), 5, 1));
        Assert.Throws<P4AmountUnknownException>(() => P4Outdoor.MonsterEvaluation(Manual, None, new Script(1), 5, 1));
        Assert.Equal(4, P4Outdoor.MonsterEvaluation(Manual, a, new Script(40, 1), 5, 2));  // 50 − 10 = 40 %: off by 1, down
        Assert.Equal(6, P4Outdoor.MonsterEvaluation(Manual, a, new Script(40, 2), 5, 2));  // up
        Assert.Equal(5, P4Outdoor.MonsterEvaluation(Manual, a, new Script(41), 5, 2));
        Assert.Equal(5, P4Outdoor.MonsterEvaluation(Manual, a, new Script(11), 5, 9));     // floor 10 %
        Assert.Equal(1, P4Outdoor.MonsterEvaluation(Manual, a, new Script(10, 1), 1, 9));  // kept within 1–12
        Assert.Equal(12, P4Outdoor.MonsterEvaluation(Manual, a, new Script(10, 2), 12, 9));
    }

    [OriginalDataFact]
    public void ManualAmounts_ShipAsTheUsersS84Values()
    {
        Assert.Equal(new P4ManualAmounts(
            ShipWreckChancePerSeaFight: 5, SpotItemIdentifyAt: 50, ExamineSearchBonus: 20, FlameballStrongArmourAt: 10,
            BegItemsPerMember: 1, DissolveRounds: 3, IdentifyChanceBase: 50, IdentifyChancePerLevel: 5,
            MonsterEvalWrongBase: 50, MonsterEvalWrongPerLevel: 5, MonsterEvalWrongMin: 10, MonsterEvalWrongBy: 1), T.Amounts);
    }

    [OriginalDataFact]
    public void D4_042_ManualStrikes_PenaltyPerStrike_SlashOneToTwoLess()
    {
        var c = T.Combat;
        Assert.Equal(90, P4Combat.MeleeHitChance(Manual, c, P4WeaponAction.Attack, 100, -5));
        Assert.Equal(100, P4Combat.MeleeHitChance(Code, c, P4WeaponAction.Attack, 100, -5));
        Assert.Equal(120, P4Combat.MeleeHitChance(Manual, c, P4WeaponAction.Slash, 150, 10)); // 150 − 20 − 10, not ÷ 3
        Assert.Equal(85, P4Combat.MeleeHitChance(Manual, c, P4WeaponAction.AimBlow, 100, 0));  // other actions as the code
        // Attack full damage under the manual; Slash 1–2 less (n − 2: n 3 → −1, n 4 → −2); Thrust keeps its +1.
        Assert.Equal(7, P4Combat.MeleeDamage(Manual, new Script(100), P4WeaponAction.Slash, 10, 2, 150));
        Assert.Equal(6, P4Combat.MeleeDamage(Manual, new Script(100), P4WeaponAction.Slash, 10, 2, 225));
        Assert.Equal(8, P4Combat.MeleeDamage(Manual, new Script(100), P4WeaponAction.Attack, 10, 2, 100));
        Assert.Equal(7, P4Combat.MeleeDamage(Code, new Script(100), P4WeaponAction.Attack, 10, 2, 100));
        Assert.Equal(9, P4Combat.MeleeDamage(Manual, new Script(100), P4WeaponAction.Thrust, 10, 2, 100));
    }

    [OriginalDataFact]
    public void D4_056_WeakAncestors_FixedByDefault_RunOffAsOption()
    {
        Assert.Equal(5.635, P4Creation.InheritanceExponent(Manual, C, 4.5));
        Assert.True(P4Creation.InheritanceExponent(Code, C, 4.5) < 1e-300); // the code: every die a 6
        // Weak dice: 6 × 0.5^5.635 ≈ 0.12 each → trunc(0.36) + 3 = 3, + Human 0 / Fighter Str 2 = 5.
        Assert.Equal(5, P4Creation.RollAbility(C, new Script(50, 50, 50), P4Ids.Human, P4Ids.Fighter, 0,
            P4Creation.InheritanceExponent(Manual, C, 4.5)));
    }
}
