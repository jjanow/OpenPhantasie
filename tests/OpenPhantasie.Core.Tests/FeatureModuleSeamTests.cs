using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Phase 09 T1.6: the feature-module seam (combat actions + damage hook) and the module registry.</summary>
public class FeatureModuleSeamTests
{
    private static readonly CombatChoice[] CombatChoicesOfI =
    [
        CombatChoice.Thrust, CombatChoice.Attack, CombatChoice.Slash, CombatChoice.Lunge, CombatChoice.Cast,
        CombatChoice.Parry,
    ];

    /// <summary>Test double: doubles every hit and records the sources it saw.</summary>
    private sealed class DoubleDamageModule : IDamageModule
    {
        public FeatureModuleDescriptor Descriptor { get; } =
            new("test.double-damage", GameId.P1, Implemented: true, "test", "test");
        public HashSet<DamageSource> Seen { get; } = [];
        public int Kills { get; private set; }

        public void BeforeDamage(DamageHit hit, List<string> log)
        {
            Seen.Add(hit.Source);
            Assert.True(hit.Member is null ^ hit.Monster is null);
            hit.Amount *= 2;
        }

        public void AfterDamage(DamageHit hit, bool killed, List<string> log)
        {
            if (killed) Kills++;
        }
    }

    /// <summary>Test double: owns PARRY (packs it as 100) and resolves packed 100 with a log line.</summary>
    private sealed class TestActionModule : ICombatActionModule
    {
        public const int Packed = 100;
        private readonly int _packed;

        public TestActionModule(int packed = Packed) => _packed = packed;

        public FeatureModuleDescriptor Descriptor { get; } =
            new("test.action", GameId.P1, Implemented: true, "test", "test");
        public IReadOnlyList<CombatChoice> Choices { get; } = [CombatChoice.Parry];

        public bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction)
        {
            packedAction = _packed;
            return request.Choice == CombatChoice.Parry;
        }

        public bool TryResolve(
            CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log)
        {
            if (packedAction != _packed) return false;
            log.Add($"{encounter.Party.Members[memberIndex].Name} TEST ACTION");
            return true;
        }
    }

    private sealed class NoRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("no roll expected");
    }

    [Fact]
    public void PhantasieI_Set_IsTheSixMenuOrders()
    {
        var set = FeatureModuleSet.PhantasieI;
        Assert.Equal(GameId.P1, set.Game);
        Assert.Equal(["p1.combat-orders"], set.Modules.Select(m => m.Descriptor.Id));
        Assert.Equal(
            [CombatChoice.Thrust, CombatChoice.Attack, CombatChoice.Slash, CombatChoice.Lunge, CombatChoice.Cast,
                CombatChoice.Parry],
            set.CombatChoices);
        Assert.Empty(FeatureModuleSet.MissingDefaults(GameId.P1));
    }

    [Fact]
    public void Registry_HomeGamesFollowTheT0Matrix()
    {
        Assert.Equal(GameId.P2, FeatureModuleRegistry.RockThrowing.HomeGame);
        Assert.Equal(GameId.P3, FeatureModuleRegistry.BodyPartInjury.HomeGame);
        Assert.Equal(GameId.P3, FeatureModuleRegistry.Bows.HomeGame);
        Assert.Equal(GameId.P3, FeatureModuleRegistry.Aim.HomeGame);
        Assert.Equal(GameId.P3, FeatureModuleRegistry.PartyRanks.HomeGame);
        Assert.True(FeatureModuleRegistry.RockThrowing.Implemented); // s79: toss-rock.md
        Assert.True(FeatureModuleRegistry.P3CombatActions.Implemented); // s88: III's own combat set
        Assert.All(FeatureModuleRegistry.All.Where(d => d.HomeGame == GameId.P3 && d.CrossGame), d => Assert.False(d.Implemented));
        Assert.Equal(FeatureModuleRegistry.All.Count, FeatureModuleRegistry.All.Select(d => d.Id).Distinct().Count());
        Assert.All(FeatureModuleRegistry.All, d => Assert.StartsWith(d.HomeGame.Prefix() + ".", d.Id));
    }

    [Fact]
    public void OtherGames_GetTheirDefaults_AndListUnbuiltOnes()
    {
        var p2 = FeatureModuleSet.ForGame(GameId.P2);
        Assert.True(p2.IsEnabled("p1.combat-orders")); // II keeps I's orders (CB pp.9-10)
        Assert.True(p2.IsEnabled("p2.rock-throwing")); // and adds "7) Toss Rock" (CB p.27)
        Assert.Equal([.. CombatChoicesOfI, CombatChoice.Toss], p2.CombatChoices);
        Assert.Empty(FeatureModuleSet.MissingDefaults(GameId.P2));

        var p3 = FeatureModuleSet.ForGame(GameId.P3);
        Assert.True(p3.IsEnabled("p3.combat-actions")); // III's own action set (s88)
        Assert.Single(p3.Modules);
        Assert.Equal(
            [CombatChoice.Attack, CombatChoice.Lunge, CombatChoice.Slash, CombatChoice.Thrust, CombatChoice.FireBow,
                CombatChoice.Parry, CombatChoice.Aim, CombatChoice.Cast, CombatChoice.ChangeRank],
            p3.CombatChoices);
        Assert.Throws<NotSupportedException>(() => FeatureModuleSet.ForGame(GameId.P1, enable: ["p3.combat-actions"]));
        Assert.Equal(
            ["p3.body-part-injury", "p3.bows", "p3.aim", "p3.party-ranks"],
            FeatureModuleSet.MissingDefaults(GameId.P3).Select(d => d.Id));
    }

    [Fact]
    public void Placeholders_CannotBeEnabled()
    {
        Assert.Throws<NotSupportedException>(() => FeatureModuleSet.ForGame(GameId.P1, enable: ["p3.aim"]));
        Assert.Throws<NotSupportedException>(() => FeatureModuleSet.ForGame(GameId.P1, enable: ["p3.body-part-injury"]));
        Assert.Throws<NotSupportedException>(() => FeatureModuleRegistry.Create("p3.bows"));
        Assert.Throws<ArgumentException>(() => FeatureModuleSet.ForGame(GameId.P1, enable: ["p9.nothing"]));
        Assert.Throws<ArgumentException>(() => FeatureModuleRegistry.Create("p9.nothing"));
    }

    [Fact]
    public void ExplicitPhantasieISet_GivesTheGoldenTrace()
    {
        foreach (var seed in new[] { 1, 77, 2026 })
            Assert.Equal(
                CombatTraceGoldenTests.RunFight(seed),
                CombatTraceGoldenTests.RunFight(seed, e => e.Modules = FeatureModuleSet.ForGame(GameId.P1)));
    }

    [Fact]
    public void DamageModule_SeesMeleeAndSpecialHits_AndChangesTheFight()
    {
        var doubler = new DoubleDamageModule();
        foreach (var seed in new[] { 1, 77, 2026 })
        {
            var hash = CombatTraceGoldenTests.RunFight(seed,
                e => e.Modules = FeatureModuleSet.Create(GameId.P1, new P1CombatOrdersModule(), doubler));
            Assert.NotEqual(CombatTraceGoldenTests.RunFight(seed), hash);
        }

        Assert.Contains(DamageSource.PartyMelee, doubler.Seen);
        Assert.Contains(DamageSource.MonsterMelee, doubler.Seen);
        Assert.Contains(DamageSource.MonsterSpecial, doubler.Seen);
        Assert.True(doubler.Kills > 0);
    }

    [Fact]
    public void DamageModule_AmountReachesHp()
    {
        var party = new Party();
        var hero = new Character
        {
            Name = "HERO", Status = CharacterStatus.Ok, HpCur = 30, HpMax = 30,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            Skills = new CharacterSkills(99, 0, 0, 0, 0, 0, 0, 0),
            WeaponDamageA = 10,
        };
        party.Members.Add(hero);
        var enc = new CombatEncounter(party);
        var mon = new Monster { Name = "RAT", HitPoints = 50, Defense = 0, Armor = 0 };
        enc.AddRank(mon);
        enc.Prepare();

        var log = new List<string>();
        CombatResolver.ResolvePartyMelee(enc, 0, CombatAction.Thrust(0), new SeededRandomSource(5), log);
        var plainLoss = 50 - mon.HitPoints;

        mon.HitPoints = 50;
        enc.Modules = FeatureModuleSet.Create(GameId.P1, new P1CombatOrdersModule(), new DoubleDamageModule());
        CombatResolver.ResolvePartyMelee(enc, 0, CombatAction.Thrust(0), new SeededRandomSource(5), log);

        Assert.True(plainLoss > 0);
        Assert.Equal(2 * plainLoss, 50 - mon.HitPoints);
    }

    [Fact]
    public void ActionModule_OwnsItsOrders_InSetOrder()
    {
        var hero = new Character { Name = "HERO", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10 };
        var prep = new CombatantPrep();
        var set = FeatureModuleSet.Create(GameId.P1, new TestActionModule(), new P1CombatOrdersModule());

        Assert.Equal(TestActionModule.Packed,
            set.Choose(new CombatOrderRequest(hero, prep, CombatChoice.Parry, 0, -1, 0), new NoRandom()));
        Assert.Equal(CombatAction.Thrust(0),
            set.Choose(new CombatOrderRequest(hero, prep, CombatChoice.Thrust, 0, -1, 0), new NoRandom()));
        Assert.Equal(CombatChoice.Parry, set.CombatChoices[0]);
    }

    [Fact]
    public void NoModuleForAnOrder_Throws()
    {
        var hero = new Character { Name = "HERO" };
        var set = FeatureModuleSet.ForGame(GameId.P1, disable: ["p1.combat-orders"]);
        Assert.Empty(set.CombatChoices);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            set.Choose(new CombatOrderRequest(hero, new CombatantPrep(), CombatChoice.Thrust, 0, -1, 0), new NoRandom()));
    }

    [Fact]
    public void ActionModule_ResolvesItsPackedAction_InTheRound()
    {
        var party = new Party();
        party.Members.Add(new Character
        {
            Name = "HERO", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10, Level = 20,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        });
        var enc = new CombatEncounter(party)
        {
            Modules = FeatureModuleSet.Create(GameId.P1, new TestActionModule(), new P1CombatOrdersModule()),
            MonstersFrozen = true,
        };
        enc.AddRank(new Monster { Name = "RAT", HitPoints = 5 });
        enc.Prepare();
        enc.SetAction(0, TestActionModule.Packed);

        var result = new CombatResolver().ResolveRound(enc, new SeededRandomSource(1));
        Assert.Contains("HERO TEST ACTION", result.Log);
    }

    [Fact]
    public void PhantasieIModule_LeavesUndecodablePackedValuesFree()
    {
        var party = new Party();
        party.Members.Add(new Character
        {
            Name = "HERO", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10, Level = 20,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        });
        Assert.False(CombatAction.TryDecodeCast(900, out _, out _));
        var enc = new CombatEncounter(party) // the test module comes after I's: I must not swallow 900 (≥ 105, no spell)
        {
            Modules = FeatureModuleSet.Create(GameId.P1, new P1CombatOrdersModule(), new TestActionModule(900)),
            MonstersFrozen = true,
        };
        enc.AddRank(new Monster { Name = "RAT", HitPoints = 5 });
        enc.Prepare();
        enc.SetAction(0, 900);

        var result = new CombatResolver().ResolveRound(enc, new SeededRandomSource(1));
        Assert.Contains("HERO TEST ACTION", result.Log);
    }

    [OriginalDataFact]
    public void Session_HandsItsModulesToEveryFight_AndChoosesThroughThem()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        Assert.Same(FeatureModuleSet.PhantasieI, session.FeatureModules);
        session.Party.Members.Add(new Character
        {
            Name = "Hero", Status = CharacterStatus.Ok, HpCur = 10, HpMax = 10,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        });
        var set = FeatureModuleSet.Create(GameId.P1, new TestActionModule(), new P1CombatOrdersModule());
        session.FeatureModules = set;

        var enc = session.BeginWildernessCombat(0);
        Assert.Same(set, enc.Modules);
        Assert.Equal(TestActionModule.Packed, session.ChooseAction(0, CombatChoice.Parry));
    }
}
