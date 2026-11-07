using System.Text.Json;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie II's Core rules (s79): Toss Rock (<c>toss-rock.md</c>), the terrain step (<c>terrain.md</c>) and Guild
/// training / level-up (<c>training.md</c>) with the user's decisions D2-013, D2-015 and D2-016. Numbers are the specs'
/// live or static values (<c>reverse-engineering/atari-st/phantasie2/</c>).
/// </summary>
public class Phantasie2RulesTests
{
    private static readonly GameTables P2 = GameTables.Load(GameId.P2);
    private static readonly GameTables P1 = GameTables.Load(GameId.P1);

    /// <summary>Replays DOS words (0..32767) in order, then repeats the last; <c>Next(0, n)</c> returns the word clamped.</summary>
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
    }

    /// <summary>The DOS word that gives <c>rnd = word / 327 = r</c>.</summary>
    private static int W(int r) => r * 327;

    // ==== Toss Rock =================================================================================================

    private static Character Thrower(string name, int toss, int weaponDamageA = 3) => new()
    {
        Name = name, Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = 3,
        Stats = new CharacterStats(13, 19, 14, 14, 18, 8), HpCur = 20, HpMax = 20,
        TossRock = toss, WeaponDamageA = weaponDamageA,
    };

    private static Monster Dog(int hp = 30, int armor = 1) =>
        new() { Name = "WILD DOG", HitPoints = hp, Armor = armor, ExperienceValue = 5 };

    private static CombatEncounter Fight(Party party, params Monster[][] ranks)
    {
        var enc = new CombatEncounter(party) { Modules = FeatureModuleSet.ForGame(GameId.P2) };
        foreach (var r in ranks)
            enc.AddRank(r);
        enc.Prepare();
        return enc;
    }

    private static Party PartyOf(params Character[] members)
    {
        var p = new Party();
        p.Members.AddRange(members);
        return p;
    }

    [Fact]
    public void Toss_IsTheSecondGamesModule_OnInIIAndAnOptionElsewhere()
    {
        var d = FeatureModuleRegistry.RockThrowing;
        Assert.True(d.Implemented);
        Assert.True(d.IsDefaultIn(GameId.P2));
        Assert.False(d.IsDefaultIn(GameId.P1));
        Assert.NotNull(FeatureModuleSet.ForGame(GameId.P2).TossRock);
        Assert.Null(FeatureModuleSet.PhantasieI.TossRock);
        var i = FeatureModuleSet.ForGame(GameId.P1, enable: ["p2.rock-throwing"]); // T5: a cross-game option in I
        Assert.Equal(CombatChoice.Toss, i.CombatChoices[^1]);
        Assert.Equal("p2.rock-throwing", GameOptions.ForPreset(GameId.P1).WithModule("p2.rock-throwing", true).Modules[^1]);
    }

    [Fact]
    public void TossPacking_IsClearOfIsOrders()
    {
        for (var rank = 0; rank <= CombatAction.MaxTossRank; rank++)
        {
            var packed = CombatAction.Toss(rank);
            Assert.True(CombatAction.TryDecodeToss(packed, out var back));
            Assert.Equal(rank, back);
            Assert.False(CombatAction.IsMelee(packed));
            Assert.False(CombatAction.TryDecodeCast(packed, out _, out _));
            Assert.False(new P1CombatOrdersModule().TryResolve(null!, 0, packed, new Words(0), []));
        }
        foreach (var other in new[] { CombatAction.Parry, CombatAction.Cleared, 10, 99, 105, 1000, 4999 })
            Assert.False(CombatAction.TryDecodeToss(other, out _));
    }

    [Fact]
    public void Toss_AnyClassPicksAnyRank_NoRoll()
    {
        var module = new P2RockThrowingModule();
        var rng = new Words(0);
        foreach (var cls in Enum.GetValues<CharacterClass>())
        {
            var ch = new Character { Class = cls };
            Assert.True(module.TryChoose(new CombatOrderRequest(ch, new CombatantPrep(), CombatChoice.Toss, 2, -1, 0), rng,
                out var packed));
            Assert.Equal(CombatAction.Toss(2), packed);
        }
        Assert.Equal(0, rng.Taken);
        Assert.False(module.TryChoose(new CombatOrderRequest(new Character(), new CombatantPrep(), CombatChoice.Thrust, 0, -1, 0),
            rng, out _));
    }

    [Fact]
    public void TargetSlot_SpreadsTheThrowersOverTheGroup_NeverPastTheLastSlot()
    {
        Assert.Equal(0, P2RockThrowingModule.TargetSlot(0, 6, 2, 0));
        Assert.Equal(2, P2RockThrowingModule.TargetSlot(5, 6, 2, 100)); // (5 + 2) / 8 · 3 = 2.625
        Assert.Equal(1, P2RockThrowingModule.TargetSlot(3, 6, 2, 0)); // 3 / 8 · 3 = 1.125
        for (var n = 1; n <= 6; n++)
        for (var i = 0; i < n; i++)
        for (var last = 0; last < 10; last++)
        for (var r = 0; r <= 100; r++)
            Assert.InRange(P2RockThrowingModule.TargetSlot(i, n, last, r), 0, last);
    }

    /// <summary>
    /// s83: the slot formula in the ST's FFP (<c>0x77bc..0x784e</c>). Oracle: PHANT.PRG's own FFP routines run in a 68000
    /// emulator over N 1..7, i &lt; N, rnd 0..100, L + 1 1..15. These 58 cases (N, i, rnd, L + 1, k) come out one below the
    /// exact floor; every other case equals it.
    /// </summary>
    private const string FfpSlotExceptions =
        "1,0,15,10,0 1,0,30,5,0 1,0,30,10,1 1,0,30,15,2 1,0,45,10,2 1,0,60,5,1 1,0,60,10,3 1,0,60,15,5 1,0,90,5,2 " +
        "1,0,90,10,5 1,0,90,15,8 2,0,60,10,2 2,1,70,5,2 2,1,70,10,5 2,1,70,15,8 4,0,30,10,0 4,0,60,5,0 4,0,60,10,1 " +
        "4,0,60,15,2 4,0,90,10,2 4,1,70,5,1 4,1,70,10,3 4,1,70,15,5 4,3,30,5,2 4,3,30,10,5 4,3,30,15,8 4,3,90,5,3 " +
        "4,3,90,10,7 4,3,90,15,11 5,1,55,10,2 5,2,5,10,2 5,3,60,5,2 5,3,60,10,5 5,3,60,15,8 5,4,10,5,2 5,4,10,10,5 " +
        "5,4,10,15,8 6,1,70,10,2 6,3,90,5,2 6,3,90,10,5 6,3,90,15,8 7,0,30,15,0 7,0,45,10,0 7,0,60,15,1 7,0,90,5,0 " +
        "7,0,90,10,1 7,0,90,15,2 7,1,70,15,3 7,1,85,10,2 7,3,30,5,1 7,3,30,10,3 7,3,30,15,5 7,3,60,15,6 7,3,90,15,7 " +
        "7,4,10,15,6 7,6,60,5,3 7,6,60,10,7 7,6,60,15,11";

    [Fact]
    public void TargetSlot_IsComputedInTheStsFfp()
    {
        var exceptions = FfpSlotExceptions.Split(' ').Select(e => e.Split(',').Select(int.Parse).ToArray())
            .ToDictionary(v => (v[0], v[1], v[2], v[3]), v => v[4]);
        var differ = 0;
        for (var n = 1; n <= 7; n++)
        for (var i = 0; i < n; i++)
        for (var r = 0; r <= 100; r++)
        for (var count = 1; count <= 15; count++)
        {
            var exact = (int)((50L * i + r) * count / (50L * (n + 2))); // floor((i + r/50) / (N + 2) · count)
            var want = exceptions.TryGetValue((n, i, r, count), out var k) ? k : exact;
            var got = P2RockThrowingModule.TargetSlot(i, n, count - 1, r);
            Assert.True(want == got, $"N {n} i {i} rnd {r} L+1 {count}: {got}, the ST gives {want}");
            if (got != exact)
                differ++;
        }
        Assert.Equal(58, differ);
        Assert.Equal(0, P2RockThrowingModule.TargetSlot(2, 6, -1, 50)); // an emptied rank: slot 0, dead
    }

    [Theory]
    // (a, b, a+b, a·b, a/b) from PHANT.PRG 0x1dd86 / 0x1e0ca / 0x1de88 run in a 68000 emulator (s83).
    [InlineData(0xFF04174Du, 0xF5BD5134u, 0xFF04174Du, 0xF4CB8141u, 0x84D4F75Au)]
    [InlineData(0xA0CBF63Eu, 0x88CE6633u, 0xA0DD103Eu, 0xABDBFE30u, 0x96723B4Cu)]
    [InlineData(0xB6539D38u, 0xAC80A13Bu, 0xC34B153Bu, 0xF5B79432u, 0x874A2A3Eu)]
    [InlineData(0x952D014Cu, 0x8DFB643Fu, 0x9531714Cu, 0xA5788E4Au, 0x867C424Eu)]
    [InlineData(0xA74E533Du, 0xCD22AC43u, 0xCFBFE543u, 0x86106140u, 0xD0CA5C3Au)]
    [InlineData(0xB3C1B341u, 0xBAD63D4Cu, 0xBAECB54Cu, 0x83312B4Du, 0xF64C7F35u)]
    [InlineData(0xF222B737u, 0x8B5BBC4Bu, 0x8B5BCB4Bu, 0x83CF9E42u, 0xDE66842Du)]
    [InlineData(0x87CCF932u, 0xF771253Du, 0xF7821F3Du, 0x8342C92Fu, 0x8C7F5B35u)]
    [InlineData(0xE2F46C44u, 0xAFB93243u, 0x9D688245u, 0x9BC94547u, 0xA5515C42u)]
    [InlineData(0xD132D441u, 0xF465534Bu, 0xF499A04Bu, 0xC7B73F4Cu, 0xDB21AD36u)]
    [InlineData(0xD24A3240u, 0xBAE8FD36u, 0xD278EC40u, 0x99894B36u, 0x9002CC4Bu)]
    [InlineData(0xE84BDB46u, 0xAF977C4Fu, 0xB00BA24Fu, 0x9F555055u, 0xA955E838u)]
    [InlineData(0xA0A4CD32u, 0xDE7F5B40u, 0xDE81DE40u, 0x8B9ED532u, 0xB8D52932u)]
    [InlineData(0xB04A2247u, 0xF26E4745u, 0xECE5B447u, 0xA6F2054Cu, 0xBA282342u)]
    [InlineData(0x88F98147u, 0x9F782041u, 0x8B776247u, 0xAAA67A47u, 0xDBE38A46u)]
    [InlineData(0xEDDFD536u, 0xB60C883Au, 0xC4EA853Au, 0xA928C630u, 0xA740523Du)]
    public void MotorolaFfp_MatchesThePrograms_Routines(uint a, uint b, uint sum, uint product, uint quotient)
    {
        Assert.Equal(sum, MotorolaFfp.Add(a, b));
        Assert.Equal(product, MotorolaFfp.Mul(a, b));
        Assert.Equal(quotient, MotorolaFfp.Div(a, b));
    }

    [Fact]
    public void MotorolaFfp_Integers_RoundTrip()
    {
        Assert.Equal(0x80000041u, MotorolaFfp.FromInt(1)); // as 0x1dbc6 gives
        Assert.Equal(0xC8000047u, MotorolaFfp.FromInt(100));
        foreach (var x in new[] { 0, 1, 2, 7, 100, 255, 65535, 1 << 20 })
            Assert.Equal(x, MotorolaFfp.ToInt(MotorolaFfp.FromInt(x)));
        Assert.Equal(0x80000041u, MotorolaFfp.Mul(MotorolaFfp.FromInt(50), MotorolaFfp.Two100ths)); // rounds to exactly 1
    }

    [Theory]
    // max(0, trunc(12·r1/100) − armor) + trunc(r2·@28/100): 0–12 from the rock less the armor, plus 0…@28.
    [InlineData(100, 100, 1, 3, 14)] // the live maximum vs WILD DOG (armor 1) for @28 3
    [InlineData(100, 100, 1, 2, 13)]
    [InlineData(50, 50, 1, 3, 6)] // 6 − 1 + 1
    [InlineData(0, 0, 1, 3, 0)] // before the 1-point floor
    [InlineData(100, 100, 12, 3, 3)] // armor ≥ 12: only the @28 part
    public void TossDamage_IsTheSpecsFormula(int r1, int r2, int armor, int a28, int expected) =>
        Assert.Equal(expected, P2RockThrowingModule.Damage(r1, r2, armor, a28));

    [OriginalDataFact]
    public void Toss_Hit_DamagesThroughTheSharedStep_RngOrderSlotHitRockStrength()
    {
        var dog = Dog(hp: 30, armor: 1);
        var glade = Thrower("GLADE", toss: 255, weaponDamageA: 3);
        var enc = Fight(PartyOf(glade), [dog]);
        var log = new List<string>();
        // slot try (r 0), hit roll (r 100 ≤ 255), rock roll (r 100 → 12), @28 roll (r 100 → 3): 12 − 1 + 3 = 14.
        var rng = new Words(W(0), W(100), W(100), W(100));
        new P2RockThrowingModule().Resolve(enc, 0, 0, rng, log);
        Assert.Equal(4, rng.Taken);
        Assert.Equal(["GLADE THROWS A ROCK", "WILD DOG TAKES 14 DAMAGE"], log);
        Assert.Equal(16, dog.HitPoints);
    }

    [OriginalDataFact]
    public void Toss_HitsOnRndAtMostToss_Toss0HitsOnlyOnZero()
    {
        foreach (var (toss, r, hits) in new[] { (0, 0, true), (0, 1, false), (40, 40, true), (40, 41, false), (100, 100, true) })
        {
            var dog = Dog();
            var enc = Fight(PartyOf(Thrower("T", toss)), [dog]);
            var log = new List<string>();
            new P2RockThrowingModule().Resolve(enc, 0, 0, new Words(W(0), W(r), W(50), W(0)), log);
            Assert.Equal(hits ? "WILD DOG TAKES 5 DAMAGE" : "T MISSES", log[^1]);
        }
    }

    [OriginalDataFact]
    public void Toss_AtAnEmptiedRank_MissesAfterThreeSlotTries_WithoutAHitRoll()
    {
        // Live (s78): DRUM (TOSS 255) tossed at rank 2 after FOREST killed its only dog this round → "MISSES".
        var dead = Dog(hp: 0);
        var enc = Fight(PartyOf(Thrower("DRUM", 255)), [Dog()], [dead]);
        var log = new List<string>();
        var rng = new Words(W(0), W(0), W(0), W(0));
        new P2RockThrowingModule().Resolve(enc, 0, 1, rng, log);
        Assert.Equal(3, rng.Taken);
        Assert.Equal(["DRUM THROWS A ROCK", "DRUM MISSES"], log);
    }

    [OriginalDataFact]
    public void Toss_LowDamage_IsFlooredAtOne_AndAKillGivesXp()
    {
        var dog = Dog(hp: 1, armor: 12);
        var enc = Fight(PartyOf(Thrower("FOREST", 255, weaponDamageA: 0)), [dog]);
        var log = new List<string>();
        new P2RockThrowingModule().Resolve(enc, 0, 0, new Words(W(0), W(0), W(0), W(0)), log);
        Assert.Equal(["FOREST THROWS A ROCK", "WILD DOG TAKES 1 DAMAGE", "WILD DOG DIES"], log);
        Assert.Equal(5, enc.RewardXp);
    }

    [OriginalDataFact]
    public void Toss_RunsThroughTheRoundLoop()
    {
        var party = PartyOf(Thrower("GLADE", 255));
        var enc = Fight(party, [Dog(hp: 200)]);
        enc.SetAction(0, CombatAction.Toss(0));
        var result = new CombatResolver().ResolveRound(enc, new SeededDosRandomSource(3));
        Assert.Contains("GLADE THROWS A ROCK", result.Log);
    }

    // ---- the TOSS skill -------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void TossRules_DataMatchesCode()
    {
        Assert.Equal(TossRockRules.PhantasieII, P2.Rules.TossRock);
        Assert.Null(P1.Rules.TossRock);
        Assert.Equal(14, P2.CreationTables.SkillCoefficientsByClassLevel[(int)CharacterClass.Monk][9][6]);
        Assert.All(Enumerable.Range(0, 15).Where(l => l != 9),
            l => Assert.Equal(0, P2.CreationTables.SkillCoefficientsByClassLevel[(int)CharacterClass.Monk][l][6]));
    }

    [OriginalDataFact]
    public void TossGrowth_TransferredGnome_Level0To1_Is77_Live()
    {
        // KLEPTO GNOME THIEF STR 18 DEX 17, TOSS 0, @62 0 → trained 0 → 1: @62 126, TOSS 77 (toss-training-live.md).
        var k = new Character { Race = Race.Gnome, Class = CharacterClass.Thief, Stats = new CharacterStats(18, 17, 17, 19, 14, 11) };
        TossRockRules.PhantasieII.Grow(k, 0, P2.CreationTables);
        Assert.Equal((126, 77), (k.TossRockBonus, k.TossRock));
    }

    [OriginalDataFact]
    public void TossGrowth_ElfWizard_Level3To4_Is59_Live()
    {
        // GLADE ELF WIZARD STR 13 DEX 14, TOSS 47 → 59, @62 85 (live save).
        var g = new Character { Race = Race.Elf, Class = CharacterClass.Wizard, Stats = new CharacterStats(13, 19, 14, 14, 18, 8), TossRock = 47 };
        TossRockRules.PhantasieII.Grow(g, 3, P2.CreationTables);
        Assert.Equal((85, 59), (g.TossRockBonus, g.TossRock));
    }

    [OriginalDataFact]
    public void TossGrowth_NewHuman_StartsAt4DexPlusStrMinus39()
    {
        var h = new Character { Race = Race.Human, Class = CharacterClass.Fighter, Stats = new CharacterStats(10, 10, 18, 10, 10, 10) };
        TossRockRules.PhantasieII.Grow(h, 0, P1.CreationTables); // I's tables carry the same column (T5: the module in I)
        Assert.Equal(43, h.TossRock);
    }

    [OriginalDataFact]
    public void Creation_GrowsTossOnlyWhileTheModuleIsOn()
    {
        CharacterCreationRequest Req(TossRockRules? toss) => new()
        {
            Name = "X", Race = Race.Dwarf, Class = CharacterClass.Fighter, TossRock = toss,
        };
        // The module switched on in I (II's own creation needs its campaign: lifespans [?]).
        var on = CharacterCreation.Create(Req(TossRockRules.PhantasieII), P1.CreationTables, new SeededDosRandomSource(4));
        Assert.Equal(Math.Max(0, 4 * on.Stats.Dex + on.Stats.Str - 39 + 15), on.TossRock);
        var off = CharacterCreation.Create(Req(null), P1.CreationTables, new SeededDosRandomSource(4));
        Assert.Equal((0, 0), (off.TossRock, off.TossRockBonus));
    }

    [Fact]
    public void TossSkill_RoundTripsInTheRemasterSave_AndIsAbsentWhenZero()
    {
        var ch = new Character { Name = "K", TossRock = 77, TossRockBonus = 126 };
        var back = GuildCodec.ReadJson(GuildCodec.WriteJson([ch], GameId.P2), GameId.P2).Single();
        Assert.Equal((77, 126), (back.TossRock, back.TossRockBonus));
        Assert.DoesNotContain("TossRock", GuildCodec.WriteJson([new Character { Name = "I" }]), StringComparison.OrdinalIgnoreCase);
        Assert.Equal((77, 126), (ch.Clone().TossRock, ch.Clone().TossRockBonus));
    }

    // ==== Training ==================================================================================================

    private static TrainingRules T2 => P2.Rules.Training!;

    [OriginalDataTheory]
    // Every class threshold seen live (toss-training-live.md §1): "You need N more experience points".
    [InlineData(CharacterClass.Monk, 18, 10, 18, 11, 11, 13, 2, 50, 2575)] // YOSHI
    [InlineData(CharacterClass.Ranger, 10, 5, 18, 18, 7, 9, 1, 0, 2399)] // FOREST, XP 0 (DOS's XP-0 rule: 479)
    [InlineData(CharacterClass.Fighter, 22, 13, 15, 20, 7, 11, 1, 10, 549)] // DRUM (I's formula: 435)
    [InlineData(CharacterClass.Priest, 20, 17, 17, 14, 17, 10, 4, 10, 6508)] // FATHER JO
    [InlineData(CharacterClass.Thief, 18, 17, 17, 19, 14, 11, 1, 10, 370)] // KLEPTO
    [InlineData(CharacterClass.Wizard, 13, 19, 14, 14, 5, 8, 2, 10, 2265)] // GLADE
    public void Training_Shortfall_IsTheLiveBox(
        CharacterClass cls, int str, int intel, int dex, int con, int cha, int luck, int level, long xp, long need)
    {
        var stats = new CharacterStats(str, intel, dex, con, cha, luck);
        Assert.Equal(need, T2.Shortfall(cls, stats, level, xp));
        var m = new Character { Class = cls, Stats = stats, Level = level, Experience = xp };
        Assert.Equal(need, TownMenus.TrainingXpShortfall(m, T2));
    }

    [OriginalDataFact]
    public void Training_Price_Level0IsFree_WizardLevel3Is66_ChaAbove20NotClamped()
    {
        Assert.Equal(0, T2.GoldCost(level: 0, charisma: 14, priceIndex: 150)); // KLEPTO, live
        Assert.Equal(66, T2.GoldCost(level: 3, charisma: 18, priceIndex: 150)); // GLADE in Pippacott, live: 11·2²·150/100
        Assert.Equal(66, T2.GoldCost(level: 3, charisma: 22, priceIndex: 150)); // (20 − 22)² = 4: the ST does not clamp
        Assert.Equal(0, TrainingRules.PhantasieI.GoldCost(level: 3, charisma: 22, priceIndex: 150)); // DOS clamps at 0
        Assert.False(T2.ClampCharisma);
    }

    [OriginalDataFact]
    public void Training_DeltasAreData_AndIsAreUnchanged()
    {
        Assert.Equal((SpellMarkRule.UpToNewLevel, HpRollTie.NewMaximum), (T2.SpellMarks, T2.HpTie));
        Assert.Equal(TrainingRules.PhantasieI, P1.Rules.Training);
        Assert.Equal((SpellMarkRule.NewLevelOnly, HpRollTie.PlusOne, true),
            (TrainingRules.PhantasieI.SpellMarks, TrainingRules.PhantasieI.HpTie, TrainingRules.PhantasieI.ClampCharisma));
    }

    [OriginalDataFact]
    public void LevelUp_MarksEveryUnknownSpellUpToTheNewLevel_Live()
    {
        // GLADE WIZARD, all flags 0, trained 3 → 4: slots 4, 5, 6, 8, 9, 12, 13, 40 (DOS's rule: only 6 and 13).
        Character Wizard() => new()
        {
            Class = CharacterClass.Wizard, Race = Race.Elf, Level = 3, Stats = new CharacterStats(13, 19, 14, 14, 18, 8),
            HpMax = 7, SpellsKnown = new int[P2.CreationTables.SpellLevelsByClass[5].Count],
        };
        var ii = Wizard();
        LevelUp.Apply(ii, 3, P2.CreationTables, new Words(0), rules: T2);
        Assert.Equal([4, 5, 6, 8, 9, 12, 13, 40], Enumerable.Range(0, ii.SpellsKnown.Length).Where(s => ii.SpellsKnown[s] == 2));
        var i = Wizard();
        LevelUp.Apply(i, 3, P2.CreationTables, new Words(0), rules: TrainingRules.PhantasieI);
        Assert.Equal([6, 13], Enumerable.Range(0, i.SpellsKnown.Length).Where(s => i.SpellsKnown[s] == 2));
    }

    [OriginalDataFact]
    public void LevelUp_AnHpRollEqualToTheOldMaximum_IsTheNewMaximumInII_PlusOneInI()
    {
        // Every d3 rolls 1 (word 0): the roll = the dice count. A FIGHTER leaving level 1 with CON 10.
        var factor = P2.CreationTables.HpFactorsByClass[(int)CharacterClass.Fighter][1];
        var dice = LevelUp.HitDiceCount(factor, 10);
        Character F() => new() { Class = CharacterClass.Fighter, Level = 1, Stats = new CharacterStats(10, 10, 10, 10, 10, 10), HpMax = dice };
        var ii = F();
        LevelUp.Apply(ii, 1, P2.CreationTables, new Words(0), rules: T2);
        Assert.Equal(dice, ii.HpMax);
        var i = F();
        LevelUp.Apply(i, 1, P2.CreationTables, new Words(0), rules: TrainingRules.PhantasieI);
        Assert.Equal(dice + 1, i.HpMax);
    }

    [OriginalDataFact]
    public void Train_D2013_CostShownWhileShort_UnlessTheStOptionHidesIt()
    {
        var town = new TownState(1, 0, "PIPPACOTT", 150, new int[TownState.StockSlotCount]);
        var m = new Character
        {
            Name = "DRUM", Class = CharacterClass.Fighter, Level = 1, Experience = 10, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(22, 13, 15, 20, 7, 11),
        };
        var party = PartyOf(m);
        var shown = TownMenus.Train(party, m, town, P2.CreationTables, new Words(0), rules: T2);
        Assert.Equal(TownTransactionKind.Failed, shown.Kind);
        Assert.StartsWith(GameText.F("core.guild.you_need_x_more_xp", 549L), shown.Message);
        Assert.Contains(GameText.F("core.guild.it_will_cost_x_gold", T2.GoldCost(1, 7, 150)), shown.Message);
        var hidden = TownMenus.Train(party, m, town, P2.CreationTables, new Words(0), rules: T2, costHiddenWhileShort: true);
        Assert.Equal(GameText.F("core.guild.you_need_x_more_xp", 549L), hidden.Message);
    }

    [OriginalDataFact]
    public void Train_Level0_IsFree_AndGrowsToss()
    {
        var town = new TownState(1, 0, "PIPPACOTT", 150, new int[TownState.StockSlotCount]);
        var k = new Character
        {
            Name = "KLEPTO", Race = Race.Gnome, Class = CharacterClass.Thief, Level = 0, Experience = 5000,
            Status = CharacterStatus.Ok, Stats = new CharacterStats(18, 17, 17, 19, 14, 11),
            SpellsKnown = new int[60], SkillBonuses = CharacterCreation.SkillBonusSeeds(new CharacterStats(18, 17, 17, 19, 14, 11)),
        };
        var party = PartyOf(k);
        var r = TownMenus.Train(party, k, town, P2.CreationTables, new Words(0), rules: T2, toss: TossRockRules.PhantasieII);
        Assert.Equal(TownTransactionKind.Ok, r.Kind);
        Assert.Equal((1, 77, 0L), (k.Level, k.TossRock, -r.GoldDelta));
        Assert.Equal(0, k.HpCur); // HP current is not raised (live: HP 0/0 → 0/4)
    }

    [OriginalDataFact]
    public void Session_WithRocksInI_GrowsTossAtCreationAndTraining()
    {
        // T5 cross-game: Phantasie I with Toss Rock switched on (Custom). The session hands the module's rules to creation
        // and training.
        var s = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(11));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1).WithModule("p2.rock-throwing", true));
        var rolled = s.RollNewMember(Race.Gnome, CharacterClass.Thief);
        Assert.Equal(Math.Max(0, 4 * rolled.Stats.Dex + rolled.Stats.Str - 39 + 30), rolled.TossRock);

        rolled.Name = "ROCKY";
        rolled.Experience = 1_000_000;
        s.Party.Members.Add(rolled);
        s.Party.GoldCarried = 15_000;
        var before = rolled.TossRock;
        Assert.Equal(TownTransactionKind.Ok, s.Train(rolled).Kind);
        Assert.Equal(2, rolled.Level);
        Assert.Equal(Math.Max(before, (before >> 1) + rolled.TossRockBonus - 49), rolled.TossRock);
        Assert.Equal(rolled.Stats.Dex * 4 + rolled.Stats.Str + 2 + 10 + 30, rolled.TossRockBonus);

        // Without the module, I's creation leaves TOSS at 0.
        var plain = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(11));
        plain.StartNewGame();
        Assert.Equal(0, plain.RollNewMember(Race.Gnome, CharacterClass.Thief).TossRock);
    }

    // ---- options ---------------------------------------------------------------------------------------------------

    [Fact]
    public void P2Options_FollowTheUsersDecisions()
    {
        var ids = RuleOptionRegistry.For(GameId.P2).Select(d => d.Id).ToArray();
        Assert.Equal(["p2.transfer-spells", "p2.transfer-divine", "p2.transfer-gold",
            "p2.training-cost", "p2.hit-dice", "p2.wizard-resurrection", "p2.autosave"], ids.Take(7));
        Assert.Equal("guild-limit", ids[^1]); // every-game options come last
        Assert.Subset(ids.ToHashSet(), new HashSet<string>
            { "p2.transport-list", "p2.inn-dead", "p2.town-inn-time", "p2.inn-magic-cap", "p2.path-time" });
        var expected = new (RuleOptionDescriptor D, string Manual, string St, string Remaster, string Diff)[]
        {
            (RuleOptionRegistry.P2TrainingCost, "manual", "st", "manual", "D2-013"),
            (RuleOptionRegistry.P2HitDice, "manual", "st", "manual", "D2-013"),
            (RuleOptionRegistry.P2WizardResurrection, "manual", "st", "manual", "D2-015"),
            (RuleOptionRegistry.P2AutoSave, "toggle", "toggle", "toggle", "D2-016"),
        };
        foreach (var (d, manual, original, remaster, diff) in expected)
            Assert.Equal((manual, original, remaster, diff), (d.Manual, d.Dos, d.Remaster, d.Difference));

        var m = GameOptions.ForPreset(GameId.P2);
        Assert.False(m.OriginalHitDice || m.TrainingCostHidden || m.OriginalSpellLevels || m.AutoSaveAlways);
        var st = GameOptions.ForPreset(GameId.P2, OptionPreset.Dos);
        Assert.True(st.OriginalHitDice && st.TrainingCostHidden && st.OriginalSpellLevels);
        Assert.False(st.AutoSaveAlways);
        Assert.True(m.With("p2.autosave", "always").AutoSaveAlways);
        Assert.Equal(OptionPreset.Custom, m.With("p2.autosave", "always").Preset); // a player value in no preset
        // I's own switches drive the same accessors; II's options don't exist in I.
        Assert.True(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).OriginalHitDice);
        Assert.False(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).TrainingCostHidden);
    }

    [Fact]
    public void P2Options_HaveLabels()
    {
        var table = StringTable.ForGame(GameId.P2);
        foreach (var d in RuleOptionRegistry.For(GameId.P2))
        {
            Assert.True(table.Contains("option." + d.Id), d.Id);
            foreach (var v in d.Values)
                Assert.True(table.Contains($"option.{d.Id}.{v}"), $"{d.Id}.{v}");
        }
    }

    [OriginalDataFact]
    public void D2015_WizardsLearnResurrectionAtLevel13_StTableAsOption()
    {
        const int resurrection = 43;
        var wizard = (int)CharacterClass.Wizard;
        Assert.Equal(13, P2.CreationTables.SpellLevelsByClass[wizard][resurrection]);
        var st = P2.CreationTables.ForRules(dosRaceModifiers: false, dosSpellLevels: true, dosLifespans: false);
        Assert.Equal(0, st.SpellLevelsByClass[wizard][resurrection]);
        Assert.Equal(13, st.SpellLevelsByClass[(int)CharacterClass.Ranger][resurrection]); // only the wizard cell moves
        Assert.Equal(9, st.SpellLevelsByClass[(int)CharacterClass.Priest][resurrection]);
        // D2-009 has no ST option: the MONK and HALFLING cells stay the manual's.
        Assert.Equal(P2.CreationTables.RaceAttributeModifiers, st.RaceAttributeModifiers);
        Assert.Equal(P2.CreationTables.SpellLevelsByClass[(int)CharacterClass.Monk], st.SpellLevelsByClass[(int)CharacterClass.Monk]);
        Assert.Null(st.LifespanFactorsByRace); // p2.lifespans stays manual here (D2-021 has its own option)
    }

    // ==== Terrain ===================================================================================================

    private static OverworldTraversal P2World() => P2.CreateTraversal();

    /// <summary>The first cell of <paramref name="tile"/> in <paramref name="regionId"/> with a cell to its west.</summary>
    private static (int X, int Y) FindCell(OverworldTraversal world, int regionId, int tile)
    {
        var region = world.GetRegion(regionId);
        for (var y = 0; y < OverworldRegion.Height; y++)
        for (var x = 1; x < OverworldRegion.Width; x++)
            if (region.GetNormalized(x, y) == tile)
                return (x, y);
        throw new InvalidOperationException($"No tile {tile} in OUT{regionId}.");
    }

    private static OverworldMoveResult StepOnto(OverworldTraversal world, OverworldState state, int regionId, int tile, IRandomSource? rng)
    {
        var (x, y) = FindCell(world, regionId, tile);
        state.RegionId = regionId;
        state.X = x - 1;
        state.Y = y;
        return world.TryMove(state, CardinalDirection.East, rng);
    }

    [OriginalDataFact]
    public void TerrainRules_AreData_IsMatchTheCode()
    {
        Assert.Equal(TerrainRules.PhantasieI, P1.Rules.Terrain);
        var t = P2.Rules.Terrain!;
        Assert.Equal((TerrainRoll.Rand327, 6), (t.Roll, t.RandomEncounterBelow));
        var fight = new TerrainClassRule(TerrainKind.TerrainFight, MonsterPerDigit: 12, MonsterOffset: 0, FightRollMax: 8, MonsterSpread: 34);
        Assert.Equal(fight, t.For(7));
        Assert.Equal(fight, t.For(11));
        Assert.Equal(new TerrainClassRule(TerrainKind.Lava, DamageDivisor: 10), t.For(8));
        Assert.Equal(TerrainKind.Swim, t.For(10).Kind);
        Assert.Equal(TerrainKind.Ordinary, t.For(9).Kind);
    }

    [OriginalDataFact]
    public void Mist_FightsAWight_OncePerTrip_Live()
    {
        // Live (terrain-live.md): a step on 112 in OUT17 started a WIGHT fight (RAM monster 26 = 0-based 25), flag 1.
        var world = P2World();
        var state = new OverworldState();
        var rng = new Words(W(0), W(34)); // r = 0 ≤ 8, r′ = 34 → 34 / 34 = 1
        var first = StepOnto(world, state, 17, 112, rng);
        Assert.Equal(OverworldMoveKind.TileClassFight, first.Kind);
        Assert.Equal(25, first.MonsterId);
        Assert.Equal("WIGHT", P2.Monsters[25].Name);
        Assert.True(state.TileClassFightDone);
        Assert.Equal(60, first.TimeCost);

        // After the fight every class-7/11 cell is fight-free until the flag is cleared; no roll is taken.
        var quiet = new Words(W(0));
        Assert.Equal(OverworldMoveKind.Moved, StepOnto(world, state, 17, 112, quiet).Kind);
        Assert.Equal(OverworldMoveKind.Moved, StepOnto(world, state, 17, 72, quiet).Kind);
        Assert.Equal(0, quiet.Taken);
    }

    [OriginalDataTheory]
    [InlineData(17, 72, 0, 24)] // LARGE SKELETON
    [InlineData(17, 72, 100, 26)] // DEATH FLY (r′ 100 → 2)
    [InlineData(18, 111, 67, 13)] // ZOMBIE (r′ 67 → 1)
    [InlineData(28, 113, 0, 36)] // DEVIL
    [InlineData(28, 74, 68, 50)] // GHOST (r′ 68 → 2)
    public void DarkVoidsAndMist_LeadMonster_Is12TPlusRPrimeOver34(int region, int tile, int rPrime, int monster)
    {
        var r = StepOnto(P2World(), new OverworldState(), region, tile, new Words(W(8), W(rPrime)));
        Assert.Equal(monster, r.MonsterId);
    }

    [OriginalDataFact]
    public void Mist_NoFightAboveR8_AndNoRandomRoll()
    {
        var state = new OverworldState();
        var rng = new Words(W(9), W(0));
        var r = StepOnto(P2World(), state, 17, 112, rng);
        Assert.Equal(OverworldMoveKind.Moved, r.Kind);
        Assert.False(state.TileClassFightDone);
        Assert.Equal(1, rng.Taken); // the 6 % roll never runs on class 7/11
    }

    [OriginalDataFact]
    public void Lava_StepAndDamage()
    {
        var world = P2World();
        Assert.Equal(OverworldMoveKind.Lava, StepOnto(world, new OverworldState(), 17, 82, new Words(0)).Kind);
        var harmless = new Words(0);
        Assert.Equal(OverworldMoveKind.Moved, StepOnto(world, new OverworldState(), 30, 80, harmless).Kind);
        Assert.Equal(0, harmless.Taken); // digit 0: no damage, no encounter roll
        Assert.Equal((1, 21), (Lava.Damage(0, 2), Lava.Damage(100, 2))); // tile 82
        Assert.Equal((1, 11, 31, 51), (Lava.Damage(0, 1), Lava.Damage(100, 1), Lava.Damage(100, 3), Lava.Damage(100, 5)));
    }

    [OriginalDataFact]
    public void Lava_BurnsEveryLivingMember_SkipsTheDead_AndCanWipe()
    {
        var a = new Character { Name = "A", Status = CharacterStatus.Ok, HpCur = 30, HpMax = 30 };
        var b = new Character { Name = "B", Status = CharacterStatus.Dead };
        var c = new Character { Name = "C", Status = CharacterStatus.Ok, HpCur = 5, HpMax = 5, Stats = new CharacterStats(10, 10, 10, 10, 10, 10) };
        var party = PartyOf(a, b, c);
        var log = new List<string>();
        var rule = P2.Rules.Terrain!.For(8);
        // Each living member: the shared routine's unused swim roll (0xfb5c), then the damage roll. A: 50·5/10 + 1 = 26; C: 51.
        var wiped = Lava.Resolve(party, 5, rule, new Words(W(7), W(50), W(7), W(100)), log);
        Assert.False(wiped);
        Assert.Equal((4, 0), (a.HpCur, c.HpCur));
        Assert.Equal(CharacterStatus.Dead, c.Status);
        Assert.Equal([" L A V A :", "A TAKES 26 DAMAGE", "C DIES"], log); // DIES is printed over C's TAKES row (0xfc92)
        Assert.True(Lava.Resolve(party, 5, rule, new Words(W(100)), []));
    }

    [OriginalDataFact]
    public void Swim_II_RollsRand327_AndCanTakeSix()
    {
        // s83 [C] 0xfac2(1): rnd = rand / 327 (0..100) < SWIM swims; else (5·rnd' + 100) / 100, so rnd' = 100 gives 6.
        var a = new Character { Name = "A", Status = CharacterStatus.Ok, HpCur = 30, HpMax = 30, Skills = new(0, 0, 0, 0, 0, 0, 0, 50) };
        var b = new Character { Name = "B", Status = CharacterStatus.Ok, HpCur = 30, HpMax = 30, Skills = new(0, 0, 0, 0, 0, 0, 0, 50) };
        var log = new List<string>();
        Swimming.Resolve(PartyOf(a, b), new Words(W(49) + 326, W(50), W(100)), log, TerrainRoll.Rand327);
        Assert.Equal([GameText.F("core.dungeon.x_swims", "A"), GameText.F("core.dungeon.x_sinks_and_takes_x_damage", "B", 6)],
            log.Skip(1));
        // s154: II's table keeps its unpadded lines (I pads the name to DOS's %-10.9s).
        Assert.Equal("A SWIMS", string.Format(GameTextSetup.PhantasieII.Get("core.dungeon.x_swims"), "A"));
        Assert.Equal("B SINKS AND TAKES 6 DAMAGE",
            string.Format(GameTextSetup.PhantasieII.Get("core.dungeon.x_sinks_and_takes_x_damage"), "B", 6));
        Assert.Equal(24, b.HpCur);
    }

    [OriginalDataFact]
    public void RandomRoll_IsRand327Below6()
    {
        var world = P2World();
        // Region 2's open land next to Pippacott: an ordinary class (0..3).
        var region = world.GetRegion(2);
        var (x, y) = (-1, -1);
        for (var yy = 0; yy < OverworldRegion.Height && x < 0; yy++)
        for (var xx = 1; xx < OverworldRegion.Width && x < 0; xx++)
        {
            var t = region.GetNormalized(xx, yy);
            if (t / 10 <= 3 && t % 10 is not (6 or 7 or 8))
                (x, y) = (xx, yy);
        }
        Assert.True(x > 0);
        OverworldMoveResult Step(int word) => world.TryMove(new OverworldState { RegionId = 2, X = x - 1, Y = y }, CardinalDirection.East, new Words(word));
        Assert.Equal(OverworldMoveKind.RandomEncounter, Step(W(5) + 326).Kind); // r = 5
        Assert.Equal(OverworldMoveKind.Moved, Step(W(6)).Kind); // r = 6
    }

    [OriginalDataFact]
    public void StepOrder_NoIiTileWhereCoreAndTheStDisagree()
    {
        // The ST tests the digit (6/7/8) before the class (5 dungeon); Core tests class 5 first. They differ only on tiles
        // 56/57/58, which II's maps don't use.
        foreach (var region in P2World().Regions)
            for (var y = 0; y < OverworldRegion.Height; y++)
            for (var x = 0; x < OverworldRegion.Width; x++)
                Assert.DoesNotContain(region.GetNormalized(x, y), new[] { 56, 57, 58 });
    }

    [OriginalDataFact]
    public void StepCosts_PathIs30_D2040_StIsTheDosTable()
    {
        var t = P2.TravelTime!;
        Assert.Equal([75, 150, 225, 300, 60, 60, 30, 60, 60, 60, 60, 60], Enumerable.Range(0, 12).Select(t.StepCost)); // D2-040 manual
        Assert.Equal(60, TravelTimeRules.Dos.StepCost(6)); // p2.path-time = st plays the ST table (= DOS I's)
        Assert.Equal(30, GameDataRepository.LoadPhantasie1().TravelTime.StepCost(6)); // I's D-055 path
    }

    [OriginalDataFact]
    public void PhantasieI_Traversal_IsUnchanged_ByTheTerrainSeam()
    {
        var p1 = GameDataRepository.LoadPhantasie1().CreateTraversal();
        Assert.Equal(TerrainRules.PhantasieI, p1.Terrain);
        Assert.Equal(57, OverworldTraversal.TileClassFightMonster(4));
    }
}
