using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D3-075 (user s209): Phantasie III's party Fireflash I–IV (<see cref="P3CombatModule"/>, <c>combat-round.md</c> section 2).
/// <c>p3.fireflash</c> = <c>manual</c> (default): a flat 1-10 / 1-30 / 1-60 / 1-100 on one front-rank monster; <c>dos</c>:
/// <c>min(rnd((INT / 2 + 1) · 1 / 4 / 8 / 13), 10 / 30 / 60 / 99)</c> on one monster of the rank the order carries (DOS action
/// code 7 / 16, root <c>0xc3ef</c> → <c>0xb42e</c>).
/// </summary>
public class P3FireflashTests
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
            LastMax = maxExclusive - 1;
            return v;
        }

        public int LastMax { get; private set; }

        public bool Done => _i == values.Length;
    }

    private sealed class EmptyItems : IItemCatalog
    {
        public ItemCatalogEntry? Get(int itemId) => null;

        public int Count => 0;
    }

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P3);
    private static readonly GameOptions Dos = GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);

    private static readonly P3HitLocationTable Locations = new P3HitLocationTable(
    [
        [69, 80, 84, 91, 91, 100],
        [17, 33, 49, 66, 95, 100],
        [3, 11, 35, 49, 80, 100],
        [40, 57, 66, 80, 89, 100],
        [0, 5, 22, 52, 70, 100],
    ]).Validate();

    private static Character Wizard(int intelligence = 9, int magic = 40)
    {
        var c = new Character
        {
            Name = "WIZ",
            Class = CharacterClass.Wizard,
            Stats = new CharacterStats(10, intelligence, 10, 10, 10, 10),
            Skills = new CharacterSkills(60, 40, 0, 0, 0, 0, 0, 0),
            HpCur = 50,
            HpMax = 50,
            MagicCur = magic,
            MagicMax = magic,
            BodyCondition = new P3BodyAreas().ToCondition(),
            SpellsKnown = new int[57],
        };
        for (var s = SpellId.FireflashI; s <= SpellId.FireflashIv; s++)
            c.SpellsKnown[s] = 1;
        return c;
    }

    private static Monster Cat(string name = "Wild Cat", int hp = 500) => new()
    {
        Id = 0, Name = name, HitPoints = hp, Defense = -22, AttackPower = 40, DamageField = 7, ExperienceValue = 10,
    };

    private static (CombatEncounter Enc, P3CombatModule Module) Fight(GameOptions rules, Character caster,
        params Monster[][] groups)
    {
        var party = new Party(RulesProfile.PhantasieI) { Death = Phantasie3DeathRule.Instance };
        party.Members.Add(caster);
        var fight = new P3Fight { HitLocations = Locations, Items = new EmptyItems() };
        var enc = new CombatEncounter(party)
        {
            Rules = rules, Modules = FeatureModuleSet.ForGame(GameId.P3), CampaignState = fight,
        };
        foreach (var g in groups)
            enc.AddRank(g);
        enc.Prepare();
        return (enc, new P3CombatModule());
    }

    private static int Cost(int spell) => Magic.MagicPointCost(spell, SpellSlots.PhantasieIII);

    [Fact]
    public void Option_IsRegistered_ManualByDefault_DosPresetPicksDos()
    {
        Assert.Contains(P3RuleOptions.Fireflash, P3RuleOptions.All);
        Assert.Equal("p3.fireflash", P3RuleOptions.Fireflash.Id);
        Assert.Equal("D3-075", P3RuleOptions.Fireflash.Difference);
        Assert.False(Manual.P3DosFireflash());
        Assert.False(GameOptions.ForPreset(GameId.P3, OptionPreset.Remaster).P3DosFireflash());
        Assert.True(Dos.P3DosFireflash());
    }

    [Theory]
    [InlineData(0, 9, 5)]    // (9 / 2 + 1) · 1
    [InlineData(1, 9, 20)]   // · 4
    [InlineData(2, 9, 40)]   // · 8
    [InlineData(3, 9, 65)]   // · 13
    [InlineData(0, 18, 10)]  // I reaches its cap at INT 18
    [InlineData(0, 17, 9)]   // 17 / 2 = 8 (integer division)
    [InlineData(1, 13, 28)]  // below II's cap of 30
    public void Dos_TopDamage_ScalesWithInt(int tier, int intelligence, int top)
    {
        var rng = new Script(top);
        Assert.Equal(top, P3CombatModule.FireflashDosDamage(tier, intelligence, rng));
        Assert.Equal(top, rng.LastMax); // the roll is rnd((INT / 2 + 1) · t): 1…top
    }

    [Theory]
    [InlineData(0, 30, 16, 10)]
    [InlineData(1, 14, 32, 30)]
    [InlineData(2, 14, 64, 60)]
    [InlineData(3, 14, 104, 99)] // IV never does 100
    [InlineData(3, 40, 273, 99)]
    public void Dos_Damage_IsCapped_10_30_60_99(int tier, int intelligence, int rolled, int capped)
    {
        Assert.Equal(capped, P3CombatModule.FireflashDosDamage(tier, intelligence, new Script(rolled)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 100)]
    public void Manual_Damage_IsFlat_NoInt(int tier, int top)
    {
        var rng = new Script(top);
        Assert.Equal(top, P3CombatModule.FireflashManualDamage(tier, rng));
        Assert.Equal(top, rng.LastMax);
    }

    [Fact]
    public void Pack_RankTwoCast_RoundTrips_AndIsDosCode16()
    {
        var packed = CombatAction.CastSpellAtRank((SpellId.FireflashI + 2), 1);
        Assert.True(CombatAction.TryDecodeCast(packed, out var spell, out var target, out var rank));
        Assert.Equal(((SpellId.FireflashI + 2), 0, 1), (spell, target, rank));
        Assert.True(CombatAction.TryDecodeCast(packed, out spell, out _));
        Assert.Equal((SpellId.FireflashI + 2), spell);
        Assert.False(CombatAction.IsHealingCast(packed));
        Assert.Equal(CombatAction.CastSpell((SpellId.FireflashI + 2)), CombatAction.CastSpellAtRank((SpellId.FireflashI + 2), 0));
        Assert.True(CombatAction.TryDecodeCast(CombatAction.CastSpell((SpellId.FireflashI + 2)), out _, out _, out rank));
        Assert.Equal(0, rank);
        Assert.Equal(16, P3CombatModule.ActionCode(packed));
        Assert.Equal(7, P3CombatModule.ActionCode(CombatAction.CastSpell((SpellId.FireflashI + 2))));
    }

    [Fact]
    public void Choose_Dos_TwoRanks_PacksThePickedRank_ManualAndOneRankDoNot()
    {
        static int Pick(CombatEncounter enc, int rank, int spell = (SpellId.FireflashI + 1)) =>
            enc.Modules.Choose(new CombatOrderRequest(enc.Party.Members[0], enc.Prep[0], CombatChoice.Cast, rank, spell, 0,
                enc.Rules, enc), new Script());

        var (dos, _) = Fight(Dos, Wizard(), [Cat()], [Cat()]);
        Assert.True(P3CombatModule.CastAsksRank(dos, (SpellId.FireflashI + 1)));
        Assert.False(P3CombatModule.CastAsksRank(dos, SpellId.QuicknessI));
        Assert.Equal(CombatAction.CastSpellAtRank((SpellId.FireflashI + 1), 1), Pick(dos, 1));
        Assert.Equal(CombatAction.CastSpell((SpellId.FireflashI + 1)), Pick(dos, 0));

        var (manual, _) = Fight(Manual, Wizard(), [Cat()], [Cat()]);
        Assert.False(P3CombatModule.CastAsksRank(manual, (SpellId.FireflashI + 1)));
        Assert.Equal(CombatAction.CastSpell((SpellId.FireflashI + 1)), Pick(manual, 1));

        var (oneRank, _) = Fight(Dos, Wizard(), [Cat()]);
        Assert.False(P3CombatModule.CastAsksRank(oneRank, (SpellId.FireflashI + 1)));
        Assert.Equal(CombatAction.CastSpell((SpellId.FireflashI + 1)), Pick(oneRank, 1));
    }

    [Fact]
    public void Dos_RankTwoOrder_HitsOneMonsterOfRankTwo_IntScaledRoll_NoInjury()
    {
        var caster = Wizard(intelligence: 9);
        var (enc, module) = Fight(Dos, caster, [Cat("Front")], [Cat("Back A"), Cat("Back B")]);
        var log = new List<string>();
        // 0xb4ec: start slot rnd(5) = 2 → slot 1 (Back B), drawn first; then rnd((9 / 2 + 1) · 4 = 20) = 17.
        var rng = new Script(2, 17);
        Assert.True(module.TryResolve(enc, 0, CombatAction.CastSpellAtRank((SpellId.FireflashI + 1), 1), rng, log));
        Assert.True(rng.Done);
        Assert.Equal(20, rng.LastMax);
        Assert.Equal(500, enc.Ranks[0].Members[0].HitPoints);
        Assert.Equal(500, enc.Ranks[1].Members[0].HitPoints);
        Assert.Equal(483, enc.Ranks[1].Members[1].HitPoints);
        Assert.Equal(40 - Cost((SpellId.FireflashI + 1)), caster.MagicCur);
        Assert.Contains(GameText.F("core.combat.x_takes_x_damage", "Back B", 17), log);
        Assert.DoesNotContain(log, l => l.Contains("injured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dos_TargetPick_WrapsPastDeadAndEmptySlots()
    {
        var (enc, module) = Fight(Dos, Wizard(), [Cat("A", hp: 0), Cat("B", hp: 0), Cat("C")]);
        // Start slot rnd(5) = 5 → slot 4 (empty), wraps to 0 and 1 (dead), lands on C; then rnd(5) = 3.
        var rng = new Script(5, 3);
        module.TryResolve(enc, 0, CombatAction.CastSpell(SpellId.FireflashI), rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(497, enc.Ranks[0].Members[2].HitPoints);
    }

    [Fact]
    public void Dos_PickedRankWiped_NothingHappens_MagicStillSpent()
    {
        var caster = Wizard();
        var (enc, module) = Fight(Dos, caster, [Cat("Front", hp: 0)], [Cat("Back")]);
        var log = new List<string>();
        var rng = new Script(); // no target roll, no damage roll, no other rank tried
        Assert.True(module.TryResolve(enc, 0, CombatAction.CastSpell(SpellId.FireflashIv), rng, log));
        Assert.Equal(500, enc.Ranks[1].Members[0].HitPoints);
        Assert.Equal(40 - Cost(SpellId.FireflashIv), caster.MagicCur);
        Assert.Single(log); // the cast line only
    }

    [Fact]
    public void Manual_HitsTheFrontRank_FlatRange_EvenForARankTwoOrder()
    {
        var caster = Wizard(intelligence: 3);
        var (enc, module) = Fight(Manual, caster, [Cat("Front A"), Cat("Front B")], [Cat("Back")]);
        // The remaster's pick: a random living monster of the front rank (Next(0, 2) = 1), then rnd(100) = 100.
        var rng = new Script(1, 100);
        module.TryResolve(enc, 0, CombatAction.CastSpellAtRank(SpellId.FireflashIv, 1), rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(400, enc.Ranks[0].Members[1].HitPoints);
        Assert.Equal(500, enc.Ranks[1].Members[0].HitPoints);
    }

    [Fact]
    public void SavedOption_KeepsARankTwoCast_AsCode16()
    {
        var caster = Wizard();
        var (enc, _) = Fight(Dos, caster, [Cat()], [Cat()]);
        var packed = CombatAction.CastSpellAtRank(SpellId.FireflashI, 1);
        enc.Prep[0].PackedAction = packed;
        P3CombatModule.SaveOrders(enc);
        Assert.Equal(16, caster.P3.SavedAction);
        Assert.Equal(packed, caster.P3.SavedCast);

        // Next fight, one group only: DOS's 0xd5c4 table leaves code 16 alone (it maps codes 1–15), so the order stays.
        var (next, _) = Fight(Dos, caster, [Cat()]);
        P3CombatModule.LoadSavedOrders(next);
        Assert.Equal(packed, next.Prep[0].PackedAction);
        Assert.Equal(16, caster.P3.SavedAction);
    }
}
