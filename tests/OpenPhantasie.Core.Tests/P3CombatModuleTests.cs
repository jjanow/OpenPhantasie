using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's combat wired into the shared round loop (<see cref="P3CombatModule"/>, s88): packing and the menu fallbacks, party
/// melee with the running sum, injury and death on both sides, monster melee against Down targets (D3-019), the bow
/// spells, and Out / Down surviving the fight's start and end. The rules themselves are pinned in
/// <see cref="Phantasie3RulesTests"/>.
/// </summary>
public class P3CombatModuleTests
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

    private sealed class Items(params (int Id, int Points)[] items) : IItemCatalog
    {
        public ItemCatalogEntry? Get(int itemId) =>
            items.Any(i => i.Id == itemId)
                ? new ItemCatalogEntry
                {
                    Id = itemId, Name = $"#{itemId}", ValueFactor = 0, Points = items.First(i => i.Id == itemId).Points,
                }
                : null;

        public int Count => items.Length;
    }

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P3);
    private static readonly GameOptions Dos = GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);

    /// <summary><c>body-areas.md</c> §1, <c>DS:0x12cc</c>.</summary>
    private static readonly P3HitLocationTable Locations = new P3HitLocationTable(
    [
        [69, 80, 84, 91, 91, 100],
        [17, 33, 49, 66, 95, 100],
        [3, 11, 35, 49, 80, 100],
        [40, 57, 66, 80, 89, 100],
        [0, 5, 22, 52, 70, 100],
    ]).Validate();

    private static Character Hero(string name = "HERO", CharacterClass cls = CharacterClass.Fighter, int attack = 120,
        int hp = 5000) => new()
    {
        Name = name,
        Class = cls,
        Stats = new CharacterStats(17, 10, 10, 17, 10, 10),
        Skills = new CharacterSkills(attack, 40, 0, 0, 0, 0, 0, 0),
        HpCur = hp,
        HpMax = hp,
        WornWeapon = 70, // 4 points (a Mace's worth)
        WornShield = 4,
        BodyCondition = new P3BodyAreas().ToCondition(),
    };

    private static Monster Cat(int hp = 50) => new()
    {
        Id = 0, Name = "Wild Cat", HitPoints = hp, Defense = -22, AttackPower = 40, DamageField = 7, ExperienceValue = 10,
    };

    private static (CombatEncounter Enc, P3Fight Fight, P3CombatModule Module) Fight(
        GameOptions rules, Character[] members, params Monster[][] groups)
    {
        var party = new Party(RulesProfile.PhantasieI) { Death = Phantasie3DeathRule.Instance };
        party.Members.AddRange(members);
        var fight = new P3Fight { HitLocations = Locations, Items = new Items((70, 4), (60, 20), (4, 4)) };
        var enc = new CombatEncounter(party)
        {
            Rules = rules, Modules = FeatureModuleSet.ForGame(GameId.P3), CampaignState = fight,
        };
        foreach (var g in groups)
            enc.AddRank(g);
        enc.Prepare();
        return (enc, fight, new P3CombatModule());
    }

    [Fact]
    public void Pack_RoundTrips_ParryIsIsParry()
    {
        foreach (var a in new[] { P3Action.Attack, P3Action.Lunge, P3Action.Slash, P3Action.Thrust, P3Action.FireBow, P3Action.Aim })
        foreach (var r in new[] { 1, 2 })
        {
            var packed = P3CombatModule.Pack(new P3ActionChoice(a, r));
            Assert.False(CombatAction.TryDecodeCast(packed, out _, out _));
            Assert.True(P3CombatModule.TryUnpack(packed, out var back));
            Assert.Equal(new P3ActionChoice(a, r), back);
        }
        Assert.Equal(CombatAction.Parry, P3CombatModule.Pack(new P3ActionChoice(P3Action.Parry)));
        Assert.False(P3CombatModule.TryUnpack(CombatAction.Thrust(0), out _));
    }

    [Fact]
    public void Choose_GoesThroughIIIsMenuRules()
    {
        var hero = Hero();
        var thief = Hero("THIEF", CharacterClass.Thief);
        var (enc, _, _) = Fight(Manual, [hero, thief], [Cat()]);
        int Pick(int member, CombatChoice c, int rank = 0) =>
            enc.Modules.Choose(new CombatOrderRequest(enc.Party.Members[member], enc.Prep[member], c, rank, -1, 0, enc.Rules,
                enc), new Script());

        // No second group: Lunge falls back to Thrust (R1); Attack at 120 % is Attack (R1).
        Assert.Equal(P3CombatModule.Pack(new(P3Action.Thrust, 1)), Pick(0, CombatChoice.Lunge));
        Assert.Equal(P3CombatModule.Pack(new(P3Action.Attack, 1)), Pick(0, CombatChoice.Attack));
        Assert.False(enc.Modules.AsksRank(enc, 1, CombatChoice.Thrust));

        // A second group: the Fighter lunges at it; a Thief is asked the rank for Thrust, a Fighter is not.
        enc.AddRank(Cat());
        Assert.Equal(P3CombatModule.Pack(new(P3Action.Lunge, 2)), Pick(0, CombatChoice.Lunge));
        Assert.True(enc.Modules.AsksRank(enc, 1, CombatChoice.Thrust));
        Assert.False(enc.Modules.AsksRank(enc, 0, CombatChoice.Thrust));
        Assert.Equal(P3CombatModule.Pack(new(P3Action.Thrust, 2)), Pick(1, CombatChoice.Thrust, rank: 1));

        // Right Arm Broken: every pick is Parry.
        var body = P3BodyAreas.Of(hero);
        body[P3BodyArea.RightArm] = P3AreaState.Broken;
        hero.BodyCondition = body.ToCondition();
        Assert.Equal(CombatAction.Parry, Pick(0, CombatChoice.Attack));
    }

    [Fact]
    public void Melee_RunningSum_LaterSwingsHitHarder()
    {
        var (enc, _, module) = Fight(Manual, [Hero(attack: 120)], [Cat(hp: 50)]);
        // Per swing: target slot rnd(5), die rnd(4), hit roll rnd(100) vs 120/2 + 22 = 82. Sums 1, then 1 + 4 = 5.
        var rng = new Script(1, 1, 50, 1, 4, 50);
        var log = new List<string>();
        Assert.True(module.TryResolve(enc, 0, P3CombatModule.Pack(new(P3Action.Attack, 1)), rng, log));
        Assert.True(rng.Done);
        Assert.Equal(50 - 1 - 5, enc.Ranks[0].Members[0].HitPoints);
        Assert.True(enc.MonsterDamaged); // Greet is now blocked (FAR:0xb618)
    }

    [Fact]
    public void Melee_RemovedTorso_KillsWithHpLeft()
    {
        var (enc, fight, module) = Fight(Manual, [Hero(attack: 60)], [Cat(hp: 6)]);
        var cat = enc.Ranks[0].Members[0];
        // Thrust (60 % < 76): target, die 4 → 1 + 4 = 5, hit; 6 − 5 = 1 left, 1 / 5 = 0 → Gone; location 5 → Torso.
        var rng = new Script(1, 4, 1, 5);
        module.TryResolve(enc, 0, P3CombatModule.Pack(new(P3Action.Thrust, 1)), rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(P3AreaState.Gone, fight.BodyOf(cat)[P3BodyArea.Torso]);
        Assert.False(cat.IsAlive);
        Assert.Equal(10, enc.RewardXp);
    }

    [Fact]
    public void Dies_AMonstersLine_RequestsSound39_AMembersNone()
    {
        var (enc, _, module) = Fight(Manual, [Hero(attack: 60)], [Cat(hp: 6)]);
        var log = new List<string>();
        module.TryResolve(enc, 0, P3CombatModule.Pack(new(P3Action.Thrust, 1)), new Script(1, 4, 1, 5), log);
        var tag = Assert.Single(enc.TakeRoundLines(), t => t.Sound == P3Sounds.Dies);
        Assert.Equal(39, P3Sounds.Dies);
        Assert.Equal(GameText.F("core.combat.x_dies", "Wild Cat"), log[tag.Line]);
        Assert.NotNull(tag.Member); // the attacker stays the line's actor

        // A member's death is damage_character (0x17e16): no 39 (s137).
        var hero = Hero(hp: 12);
        hero.Skills = hero.Skills with { Parry = 0 };
        var (enc2, fight2, _) = Fight(Manual, [hero], [Cat()]);
        var log2 = new List<string>();
        P3CombatModule.HitMember(enc2, fight2, hero, 6, new Script(50), log2);
        P3CombatModule.HitMember(enc2, fight2, hero, 5, new Script(1), log2);
        Assert.Contains(GameText.F("core.combat.x_dies", hero.Name), log2);
        Assert.DoesNotContain(enc2.TakeRoundLines(), t => t.Sound == P3Sounds.Dies);
    }

    [Fact]
    public void Dies_APartyFireflashKill_RequestsSound39()
    {
        // s138: 39's fifth caller 0x187ce is in 0x186f6, the party's Fireflash I-IV (spells #5-8 at 0x168f2, orders 7 / 16 =
        // Cast at rank 1 / 2): damage_monster with the flash set, then " dies!" with 39. Core's cast reaches TagDies too.
        var caster = Hero("CASTER", CharacterClass.Wizard);
        caster.SpellsKnown = new int[57];
        caster.SpellsKnown[SpellId.FireflashI] = 1;
        caster.MagicCur = caster.MagicMax = 20;
        var (enc, _, module) = Fight(Manual, [caster], [Cat(hp: 1)]);
        var log = new List<string>();
        Assert.True(module.TryResolve(enc, 0, CombatAction.CastSpell(SpellId.FireflashI), new Script(5), log)); // D3-075 manual: rnd(10)
        var tag = Assert.Single(enc.TakeRoundLines(), t => t.Sound == P3Sounds.Dies);
        Assert.Equal(GameText.F("core.combat.x_dies", "Wild Cat"), log[tag.Line]);
    }

    [Fact]
    public void D3019_MonsterMelee_DownTargets_DosAlwaysHit_ManualStillDodges()
    {
        static Character DownHero()
        {
            var h = Hero();
            h.Skills = h.Skills with { Parry = 400 }; // defence far above the cat's 40
            h.Status = CharacterStatus.Down;
            var b = P3BodyAreas.Of(h);
            b[P3BodyArea.LeftLeg] = P3AreaState.Broken;
            h.BodyCondition = b.ToCondition();
            return h;
        }

        // DOS: chance 100. One attack ((40 + 80) / 115 = 1); a one-member party draws no target roll.
        var (enc, _, module) = Fight(Dos, [DownHero()], [Cat()]);
        var rng = new Script(100, 3);
        Assert.True(module.TryMonsterMelee(enc, enc.Ranks[0], enc.Ranks[0].Members[0], MonsterMeleeRider.None, rng,
            new List<string>()));
        Assert.True(rng.Done);
        Assert.Equal(5000 - 3, enc.Party.Members[0].HpCur);

        // Manual: double a chance below 0 is still 0, so the roll of 1 misses.
        (enc, _, module) = Fight(Manual, [DownHero()], [Cat()]);
        rng = new Script(1);
        module.TryMonsterMelee(enc, enc.Ranks[0], enc.Ranks[0].Members[0], MonsterMeleeRider.None, rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(5000, enc.Party.Members[0].HpCur);
    }

    [Fact]
    public void MonsterMelee_TheRunStopsAtTheFirstMiss()
    {
        var hero = Hero();
        hero.Skills = hero.Skills with { Parry = 0 };
        var (enc, _, module) = Fight(Manual, [hero], [Cat()]);
        var ogre = enc.Ranks[0].Members[0];
        ogre.AttackPower = 300; // (300 + 80) / 115 = 3 swings, chance 100 − defence each
        // Swing 1 hits (roll 1, damage 2), swing 2 misses (roll 100): swing 3 never comes.
        var rng = new Script(1, 2, 100);
        module.TryMonsterMelee(enc, enc.Ranks[0], ogre, MonsterMeleeRider.None, rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(5000 - 2, hero.HpCur);
    }

    [Fact]
    public void D3036_Parry_SkillRoll_ShieldOffTheChance_AndOffTheFirstHit()
    {
        var hero = Hero(); // Parry 40, shield #4 = 4 points
        var (enc, fight, module) = Fight(Manual, [hero], [Cat()]);
        enc.Prep[0].PackedAction = CombatAction.Parry;
        var cat = enc.Ranks[0].Members[0];
        var log = new List<string>();

        P3CombatModule.RollParries(enc, fight, new Script(41), log); // 41 > 40: the parry fails
        Assert.False(fight.Parried(hero));
        P3CombatModule.RollParries(enc, fight, new Script(40), log);
        Assert.True(fight.Parried(hero));
        Assert.Contains("HERO parries.", log);

        // The first damaging hit loses the shield's 4 points; the second hit this round is whole.
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(1, 7), log);
        Assert.Equal(5000 - 3, hero.HpCur);
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(1, 7), log);
        Assert.Equal(5000 - 3 - 7, hero.HpCur);

        // A hit no bigger than the shield is blocked outright.
        fight.ClearParries();
        fight.SetParried(hero);
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(1, 4), log);
        Assert.Equal(5000 - 10, hero.HpCur);
        Assert.Contains("HERO blocks the blow with the shield!", log);

        // The shield also comes off the chance: a roll that hits a non-parrying hero misses a parrying one.
        var defence = P3Combat.Defence(4, 0, hero.Skills.Parry, hero.Stats.Dex, Manual);
        var open = P3Combat.MonsterChance(cat.AttackPower, defence, false, 0, false, 4, P3Status.Okay, Manual);
        Assert.Equal(open - 4, P3Combat.MonsterChance(cat.AttackPower, defence, false, 0, true, 4, P3Status.Okay, Manual));
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(open), log);
        Assert.Equal(5000 - 10, hero.HpCur);
    }

    [Fact]
    public void D3036_DosParry_IsLastFightsOrder_NoHitReduction()
    {
        var hero = Hero();
        var (enc, fight, module) = Fight(Dos, [hero], [Cat()]);
        var cat = enc.Ranks[0].Members[0];
        var defence = P3Combat.Defence(4, 0, hero.Skills.Parry, hero.Stats.Dex, Dos);
        var open = P3Combat.MonsterChance(cat.AttackPower, defence, false, 0, false, 4, P3Status.Okay, Dos);

        // This fight's Parry order means nothing; the saved option does, all fight.
        enc.Prep[0].PackedAction = CombatAction.Parry;
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(open, 7), new List<string>());
        Assert.Equal(5000 - 7, hero.HpCur); // whole damage
        P3CombatModule.SaveOrders(enc); // fight end: +0x6e = Parry
        Assert.True(hero.P3.ParrySaved);
        module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(open), new List<string>());
        Assert.Equal(5000 - 7, hero.HpCur); // the same roll now misses
        Assert.False(fight.Parried(hero));
    }

    // ---- The saved option at fight start (root 0xd55b) ----------------------------------------------------------------

    [Fact]
    public void FightStart_SavedOption_IsRechecked_AndBecomesTheFirstOrder()
    {
        var weak = Hero("WEAK", attack: 60);
        weak.P3.SavedAction = (int)P3Action.Attack; // Attack below 76 % → Thrust
        var slasher = Hero("SLASH", attack: 120);
        slasher.P3.SavedAction = (int)P3Action.Slash; // Slash below 150 % → Thrust
        var archer = Hero("BOW");
        archer.P3.SavedAction = 15; // Fire Bow (R2) with no bow → Thrust
        var hurt = Hero("ARM");
        hurt.P3.SavedAction = (int)P3Action.Attack;
        var body = P3BodyAreas.Of(hurt);
        body[P3BodyArea.RightArm] = P3AreaState.Broken; // → Parry
        hurt.BodyCondition = body.ToCondition();
        var (enc, _, _) = Fight(Manual, [weak, slasher, archer, hurt], [Cat()], [Cat()]);

        P3CombatModule.LoadSavedOrders(enc);

        var thrust = P3CombatModule.Pack(new P3ActionChoice(P3Action.Thrust, 1));
        Assert.Equal(thrust, enc.Prep[0].PackedAction);
        Assert.Equal(thrust, enc.Prep[1].PackedAction);
        Assert.Equal(thrust, enc.Prep[2].PackedAction);
        Assert.Equal(CombatAction.Parry, enc.Prep[3].PackedAction);
        Assert.Equal((int)P3Action.Thrust, weak.P3.SavedAction); // written back to +0x6e
        Assert.True(hurt.P3.ParrySaved);
    }

    [Fact]
    public void FightStart_NoSecondGroup_RankTwoOrdersDropToRankOne_ButTheSavedOptionKeepsThem()
    {
        var slasher = Hero("SLASH", attack: 160);
        slasher.P3.SavedAction = 13; // Slash (R2)
        var lunger = Hero("LUNGE");
        lunger.P3.SavedAction = 12; // Lunge (R2) → Thrust
        var caster = Hero("CAST");
        caster.P3.SavedAction = 7;
        caster.P3.SavedCast = CombatAction.CastSpell(SpellId.FireflashI);
        var fresh = Hero("NEW"); // creation: Thrust
        var (enc, _, _) = Fight(Manual, [slasher, lunger, caster, fresh], [Cat()]);

        P3CombatModule.LoadSavedOrders(enc);

        Assert.Equal(P3CombatModule.Pack(new P3ActionChoice(P3Action.Slash, 1)), enc.Prep[0].PackedAction);
        Assert.Equal(13, slasher.P3.SavedAction);
        Assert.Equal(P3CombatModule.Pack(new P3ActionChoice(P3Action.Thrust, 1)), enc.Prep[1].PackedAction);
        Assert.Equal(CombatAction.CastSpell(SpellId.FireflashI), enc.Prep[2].PackedAction);
        Assert.Equal(P3CombatModule.Pack(new P3ActionChoice(P3Action.Thrust, 1)), enc.Prep[3].PackedAction);
    }

    [Fact]
    public void FightEnd_SavesTheLastOrderAsADosCode_AnEmptyOrderKeepsTheOldOne()
    {
        var a = Hero("A");
        var b = Hero("B");
        b.P3.SavedAction = (int)P3Action.Aim;
        var c = Hero("C");
        var (enc, _, _) = Fight(Manual, [a, b, c], [Cat()], [Cat()]);
        enc.Prep[0].PackedAction = P3CombatModule.Pack(new P3ActionChoice(P3Action.FireBow, 2));
        enc.Prep[1].PackedAction = CombatAction.Cleared;
        enc.Prep[2].PackedAction = CombatAction.CastSpell(SpellId.FireflashI);

        P3CombatModule.SaveOrders(enc);

        Assert.Equal(15, a.P3.SavedAction);
        Assert.Equal((int)P3Action.Aim, b.P3.SavedAction);
        Assert.Equal(7, c.P3.SavedAction);
        Assert.Equal(CombatAction.CastSpell(SpellId.FireflashI), c.P3.SavedCast);
    }

    [Fact]
    public void MonsterMelee_AnUnconsciousMonster_MakesNoAttack()
    {
        var (enc, fight, module) = Fight(Manual, [Hero()], [Cat()]);
        var cat = enc.Ranks[0].Members[0];
        fight.BodyOf(cat)[P3BodyArea.Head] = P3AreaState.Broken;
        Assert.Equal(P3Status.Out, fight.StatusOf(cat));
        Assert.True(module.TryMonsterMelee(enc, enc.Ranks[0], cat, MonsterMeleeRider.None, new Script(), new List<string>()));
    }

    [Fact]
    public void MonsterHits_BreakALeg_Down_OrTakeTheHead_Dead()
    {
        var hero = Hero(hp: 12);
        hero.Skills = hero.Skills with { Parry = 0 };
        var (enc, fight, _) = Fight(Manual, [hero], [Cat()]);
        // 12 − 6 = 6 left, 6 / 6 = 1 → Broken; location 50 → Left Leg.
        P3CombatModule.HitMember(enc, fight, hero, 6, new Script(50), new List<string>());
        Assert.Equal(CharacterStatus.Down, hero.Status);
        Assert.Equal(P3AreaState.Broken, P3BodyAreas.Of(hero)[P3BodyArea.LeftLeg]);

        // 6 − 5 = 1 left, 1 / 5 = 0 → Gone; location 1 → Head: dead with 1 HP left.
        P3CombatModule.HitMember(enc, fight, hero, 5, new Script(1), new List<string>());
        Assert.Equal(CharacterStatus.Dead, hero.Status);
    }

    [Fact]
    public void DeathKeepsCon_ResurrectionTakesOneOrTwo_FloorThree()
    {
        var hero = Hero(hp: 5);
        hero.Stats = hero.Stats with { Con = 12 };
        var (enc, fight, _) = Fight(Manual, [hero], [Cat()]);
        // 5 − 5 = 0: dead; the injury roll (0 / 5 = 0 → Gone) lands on row 2 at 50 → Left Leg.
        P3CombatModule.HitMember(enc, fight, hero, 5, new Script(50), new List<string>());
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(12, hero.Stats.Con); // III: no CON lost at death

        P3Spells.AfterResurrection(Manual, hero, new Script(2));
        Assert.Equal((CharacterStatus.Ok, 1, 10), (hero.Status, hero.HpCur, hero.Stats.Con));
        Assert.True(P3BodyAreas.Of(hero).AllOkay);

        hero.Stats = hero.Stats with { Con = 4 };
        P3Spells.AfterResurrection(Manual, hero, new Script(2));
        Assert.Equal(3, hero.Stats.Con);
    }

    [Fact]
    public void CastRefusals_AreThrustR1_AsDosRoot0xfc41()
    {
        // s128 [C] static: #52 / #54 / #57+ "cannot be cast in combat", cost above magic "too low", Healing on a Dead
        // member "dead or out": each a message and action 3 = Thrust (R1) (0xff55); before s128 Core rolled I's Parry.
        var caster = Hero("CASTER", CharacterClass.Priest);
        caster.SpellsKnown = new int[57];
        foreach (var id in new[] { SpellId.HealingI, SpellId.FireflashI, 51, 53, 56 })
            caster.SpellsKnown[id] = 1;
        caster.MagicCur = caster.MagicMax = 20;
        var dead = Hero("DEAD");
        dead.Status = CharacterStatus.Dead;
        var (enc, _, _) = Fight(Manual, [caster, dead], [Cat()]);
        int Cast(int spell, int target = 0) =>
            enc.Modules.Choose(new CombatOrderRequest(caster, enc.Prep[0], CombatChoice.Cast, 0, spell, target, enc.Rules, enc),
                new Script(99, 99, 99));
        var thrust = P3CombatModule.Pack(new(P3Action.Thrust, 1));

        foreach (var id in new[] { 51, 53, 56 })
        {
            Assert.Equal("core.p3.spell_not_in_combat", P3CombatModule.CastRefusal(caster, id, null, Manual));
            Assert.Equal(thrust, Cast(id));
        }
        Assert.Null(P3CombatModule.CastRefusal(caster, SpellId.FireflashI, null, Manual));
        Assert.Equal(CombatAction.CastSpell(SpellId.FireflashI), Cast(SpellId.FireflashI));
        Assert.Equal(CombatAction.CastHealing(SpellId.HealingI, 0), Cast(SpellId.HealingI, 0));
        Assert.Equal("core.p3.dead_or_out", P3CombatModule.CastRefusal(caster, SpellId.HealingI, dead, Manual));
        Assert.Equal(thrust, Cast(SpellId.HealingI, 1));
        Assert.Equal(thrust, Cast(SpellId.FireflashI + 1)); // Fireflash II: not known

        caster.MagicCur = 0;
        Assert.Equal("core.p3.magic_too_low", P3CombatModule.CastRefusal(caster, SpellId.FireflashI, null, Manual));
        Assert.Equal(thrust, Cast(SpellId.FireflashI));
    }

    [Fact]
    public void CastRefusalBeforeList_NoSpellsFirst_ThenMagicZero()
    {
        // s134 [C] static: DOS 0xffef / 0xffdf and Amiga 0x19430 / 0x1943a refuse Cast before the list opens: no known
        // spell at all (any spell counts, combat or not), then current magic <= 0.
        var ch = Hero("FIGHTER");
        ch.SpellsKnown = new int[57];
        ch.MagicCur = 0;
        Assert.Equal("core.p3.knows_no_spells", P3CombatModule.CastRefusalBeforeList(ch));
        ch.SpellsKnown[SpellId.Transportation] = 1; // a non-combat spell still counts
        Assert.Equal("core.p3.magic_too_low", P3CombatModule.CastRefusalBeforeList(ch));
        ch.MagicCur = 1;
        Assert.Null(P3CombatModule.CastRefusalBeforeList(ch));
        ch.MagicCur = -1;
        Assert.Equal("core.p3.magic_too_low", P3CombatModule.CastRefusalBeforeList(ch));
    }

    [Fact]
    public void CastGate_NoLivingMonster_OnlyHealingAndResurrectionGoAhead()
    {
        // DOS III 0xce66 [C]: non-healing spells need FAR:0xe5b2 / 0xe63c (the groups' living counts) above 0.
        Character Caster()
        {
            var c = Hero("CASTER", CharacterClass.Priest);
            c.SpellsKnown = new int[57];
            c.SpellsKnown[SpellId.HealingI] = 1;
            c.SpellsKnown[SpellId.FireflashI] = 1;
            c.SpellsKnown[13] = 1; // P. Arrow Flame I (bow-spell path)
            c.MagicCur = c.MagicMax = 20;
            return c;
        }

        foreach (var spell in new[] { SpellId.FireflashI, 13 })
        {
            var caster = Caster();
            var (enc, _, module) = Fight(Manual, [caster], [Cat(hp: 5)]);
            enc.Ranks[0].Members[0].HitPoints = 0; // the last monster died earlier in the round
            var log = new List<string>();
            Assert.True(module.TryResolve(enc, 0, CombatAction.CastSpell(spell), new Script(1, 1, 1, 1), log));
            Assert.Empty(log);
            Assert.Equal(20, caster.MagicCur);
        }

        var healer = Caster();
        var (healEnc, _, healModule) = Fight(Manual, [healer], [Cat(hp: 5)]);
        healEnc.Ranks[0].Members[0].HitPoints = 0;
        var healLog = new List<string>();
        healModule.TryResolve(healEnc, 0, CombatAction.CastHealing(SpellId.HealingI, 0), new Script(1, 1, 1, 1), healLog);
        Assert.NotEmpty(healLog);
        Assert.True(healer.MagicCur < 20);
        Assert.True(P3CombatModule.CastGoesAhead(healEnc, SpellId.Resurrection));
    }

    [Fact]
    public void PartyCastLine_NamesTheSpell_OnBothCastPaths()
    {
        // DOS III party_casts (root 0xbf9a) [C]: name + " casts " + master string 331 + spell; the Amiga prints the same.
        Character Caster()
        {
            var c = Hero("CASTER", CharacterClass.Wizard);
            c.SpellsKnown = new int[57];
            c.SpellsKnown[SpellId.FireflashI] = 1;
            c.SpellsKnown[13] = 1; // #14 P. Arrow Flame I (III's bow-spell path)
            c.MagicCur = c.MagicMax = 20;
            return c;
        }
        string[] names = [.. Enumerable.Range(0, 57).Select(i => $"Spell{i}")];

        foreach (var spell in new[] { SpellId.FireflashI, 13 })
        {
            var (enc, _, module) = Fight(Manual, [Caster()], [Cat(hp: 500)]);
            enc.CampaignState = new P3Fight { HitLocations = Locations, Items = new Items(), SpellNames = names };
            var log = new List<string>();
            module.TryResolve(enc, 0, CombatAction.CastSpell(spell), new Script(1, 1, 1, 1, 1, 1), log);
            Assert.Equal(GameText.F("core.p3.x_casts_x", "CASTER", names[spell]), log[0]);
            // s127: the Amiga starts sound 21 with it (0x1620a).
            Assert.Contains(enc.TakeRoundLines(), t => t.Line == 0 && t.Sound == P3Fight.CastSound);
        }

        // No names (a fight built without them): I's line.
        var (plain, _, m2) = Fight(Manual, [Caster()], [Cat(hp: 500)]);
        var plainLog = new List<string>();
        m2.TryResolve(plain, 0, CombatAction.CastSpell(13), new Script(), plainLog);
        Assert.Equal(GameText.F("core.combat.x_casts_a_spell", "CASTER"), plainLog[0]);
    }

    [Fact]
    public void D3017_PartyArrowFlame_LastsTheFight_AndFeedsFireBow()
    {
        var archer = Hero("ARCHER");
        archer.WornBow = 60; // 20 points
        archer.P3.FireBow = 60;
        var caster = Hero("CASTER", CharacterClass.Wizard);
        caster.SpellsKnown = new int[57];
        caster.SpellsKnown[13] = 1; // #14 P. Arrow Flame I
        caster.MagicCur = caster.MagicMax = 20;
        var (enc, fight, module) = Fight(Manual, [archer, caster], [Cat(hp: 500)]);

        module.TryResolve(enc, 1, CombatAction.CastSpell(13), new Script(), new List<string>());
        Assert.Equal(P3BowSpell.ArrowFlameI, fight.BowSpellOf(archer));
        Assert.Equal(P3BowSpell.ArrowFlameI, fight.BowSpellOf(caster));
        Assert.True(caster.MagicCur < 20);

        // Fire Bow: 110 % − (−22) / 2 = 121; range 2·(20 + 15) + 4 = 74 (manual). Target, hit roll, damage 30.
        var rng = new Script(1, 100, 30);
        module.TryResolve(enc, 0, P3CombatModule.Pack(new(P3Action.FireBow, 1)), rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(470, enc.Ranks[0].Members[0].HitPoints);
    }

    [Fact]
    public void Accuracy55_SetsCastersBowTo250_Costs3_PartyAccuracy56_EveryMember_Costs4()
    {
        var archer = Hero("ARCHER");
        archer.WornBow = 60; // 20 points
        archer.P3.FireBow = 60;
        var caster = Hero("CASTER", CharacterClass.Wizard);
        caster.SpellsKnown = new int[57];
        caster.SpellsKnown[P3Spells.Accuracy] = 1;
        caster.SpellsKnown[P3Spells.PartyAccuracy] = 1;
        caster.MagicCur = caster.MagicMax = 20;
        var (enc, fight, module) = Fight(Manual, [archer, caster], [Cat(hp: 500)]);

        // The order goes through the shared chooser with III's layout (index 54 is not I's DIVINE I here).
        var order = CombatOrders.Choose(caster, enc.Prep[1], CombatChoice.Cast, new Script(), 0, P3Spells.Accuracy,
            rules: Manual);
        Assert.Equal(CombatAction.CastSpell(P3Spells.Accuracy), order);
        module.TryResolve(enc, 1, order, new Script(), new List<string>());
        Assert.Equal(17, caster.MagicCur); // slot 54: 54 % 4 + 1 = 3
        Assert.True(fight.HasAccuracy(caster));
        Assert.False(fight.HasAccuracy(archer));
        Assert.Equal(new P3BowStats(250, 4), fight.BowStatsOf(caster, Manual)); // no bow: range 4

        module.TryResolve(enc, 1, CombatAction.CastSpell(P3Spells.PartyAccuracy), new Script(), new List<string>());
        Assert.Equal(13, caster.MagicCur); // slot 55: 4 MP
        Assert.Equal(new P3BowStats(250, 44), fight.BowStatsOf(archer, Manual)); // 2·20 + 4

        // Fire Bow: 250 − (−22) / 2 = 261: target, hit roll 100, damage 30.
        var rng = new Script(1, 100, 30);
        module.TryResolve(enc, 0, P3CombatModule.Pack(new(P3Action.FireBow, 1)), rng, new List<string>());
        Assert.True(rng.Done);
        Assert.Equal(470, enc.Ranks[0].Members[0].HitPoints);
    }

    [Fact]
    public void Accuracy_WritesOnlyTheBowPercent_ArrowFlameWritesBoth()
    {
        var archer = Hero("ARCHER");
        archer.WornBow = 60; // 20 points
        archer.P3.FireBow = 60;
        var dos = Dos.With("p3.arrow-flame", "manual"); // D3-039 dos, the damage by D3-017's manual range
        var (_, fight, _) = Fight(dos, [archer], [Cat()]);

        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameI, dos);
        fight.SetBowSpell(archer, P3BowSpell.Accuracy, dos); // 0xca34: % word only, Arrow Flame's damage stays
        Assert.Equal(new P3BowStats(250, 74), fight.BowStatsOf(archer, dos));
        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameII, dos); // 0xc3cc writes both words: 250 gone
        Assert.Equal(new P3BowStats(160, 94), fight.BowStatsOf(archer, dos));
    }

    [Fact]
    public void BowSpells_Manual_NoneLowersAnEarlierOne_D3039()
    {
        var archer = Hero("ARCHER");
        archer.WornBow = 60; // 20 points
        archer.P3.FireBow = 60;
        var (_, fight, _) = Fight(Manual, [archer], [Cat()]);

        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameI, Manual);
        fight.SetBowSpell(archer, P3BowSpell.Accuracy, Manual);
        Assert.Equal(new P3BowStats(250, 74), fight.BowStatsOf(archer, Manual));
        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameII, Manual); // the 250 stays, II's damage comes in
        Assert.Equal(new P3BowStats(250, 94), fight.BowStatsOf(archer, Manual));
        fight.SetBowSpell(archer, P3BowSpell.ArrowFlameI, Manual); // I does not replace II
        Assert.Equal(new P3BowStats(250, 94), fight.BowStatsOf(archer, Manual));

        // Accuracy over Arrow Flame II with Fire Bow 200: 300 stays (DOS lowers it to 250).
        archer.P3.FireBow = 200;
        Assert.Equal(300, fight.BowStatsOf(archer, Manual).Percent);
        Assert.Equal(250, fight.BowStatsOf(archer, Dos).Percent);
    }

    [Fact]
    public void SpellSlots_PerGame_ExtendPastIs55()
    {
        Assert.Equal(55, SpellSlots.PhantasieI.Count);
        Assert.Equal(2, SpellSlots.PhantasieI.MagicPointCost(SpellId.DivineI)); // slot 57
        Assert.False(SpellSlots.PhantasieI.Contains(55));
        Assert.Equal(56, SpellSlots.PhantasieII.Count);
        Assert.Equal(1, SpellSlots.PhantasieII.MagicPointCost(SpellId.DivineIi)); // slot 56
        Assert.Equal(57, SpellSlots.For(GameId.P3).Count);
        Assert.Equal(3, SpellSlots.For(Manual).MagicPointCost(P3Spells.Accuracy));
        Assert.Equal(4, SpellSlots.For(Manual).MagicPointCost(P3Spells.PartyAccuracy));
        Assert.Same(SpellSlots.PhantasieI, SpellSlots.For((GameOptions?)null));
        for (var i = 0; i < SpellSlots.PhantasieII.Count; i++) // the I / II default keeps its old costs
            Assert.Equal(Magic.MagicPointCost(i), SpellSlots.PhantasieII.MagicPointCost(i));

        // Accuracy is combat-only in III (C/P); index 54 outside III stays I's DIVINE I (cast anywhere).
        Assert.True(SpellCaster.IsCombatOnly(P3Spells.Accuracy, Manual));
        Assert.False(SpellCaster.IsCombatOnly(SpellId.DivineI));

        // A I-rules spend refuses index 55; III's layout takes it.
        var c = Hero("C", CharacterClass.Wizard);
        c.SpellsKnown = new int[57];
        c.SpellsKnown[55] = 1;
        c.MagicCur = c.MagicMax = 10;
        Assert.Equal(-1, Magic.TrySpendMp(c, 55, out _));
        Assert.Equal(4, Magic.TrySpendMp(c, 55, out _, SpellSlots.PhantasieIII));
    }

    [Fact]
    public void SpellSlots_II_DivineIiInCombat_SpendsOneMpForNothing()
    {
        // Side effect of II's layout (56 spells): DIVINE II packs as a cast, not the parry path, and costs slot 56's 1 MP.
        var ii = GameOptions.ForPreset(GameId.P2);
        var c = Hero("C", CharacterClass.Wizard);
        c.SpellsKnown = new int[56];
        c.SpellsKnown[SpellId.DivineIi] = 1;
        c.MagicCur = c.MagicMax = 10;
        var party = new Party();
        party.Members.Add(c);
        var enc = new CombatEncounter(party) { Rules = ii };
        enc.AddRank([Cat()]);
        enc.Prepare();
        var order = CombatOrders.Choose(c, enc.Prep[0], CombatChoice.Cast, new Script(), 0, SpellId.DivineIi, rules: ii);
        Assert.Equal(CombatAction.CastSpell(SpellId.DivineIi), order);
        var log = new List<string>();
        var r = SpellCaster.ResolveCombatCast(enc, 0, order, new Script(), log);
        Assert.True(r.Success);
        Assert.Equal(9, c.MagicCur);
        Assert.Equal([GameText.F("core.combat.x_casts_a_spell", "C")], log);
    }

    [Fact]
    public void PartyQuickness_And_Protection_RebuildTheFightValues_UndoingASlow()
    {
        var fighter = Hero("FIGHTER");
        fighter.WornArmor = 4; // 4 points in this catalog
        var caster = Hero("CASTER", CharacterClass.Wizard);
        caster.SpellsKnown = new int[57];
        caster.SpellsKnown[(SpellId.QuicknessI + 1)] = 1;
        caster.SpellsKnown[(SpellId.ProtectionI + 2)] = 1;
        caster.MagicCur = caster.MagicMax = 20;
        var (enc, fight, module) = Fight(Manual, [fighter, caster], [Cat(hp: 500)]);
        fight.SetAttack(fighter, 24); // slowed to 20 %
        fight.SetArmour(fighter, 1);

        module.TryResolve(enc, 1, CombatAction.CastSpell((SpellId.QuicknessI + 1)), new Script(), new List<string>());
        Assert.Equal(120 + 25, fight.AttackOf(fighter));
        module.TryResolve(enc, 1, CombatAction.CastSpell((SpellId.ProtectionI + 2)), new Script(), new List<string>());
        Assert.Equal(4 + 6, fight.ArmourOf(fighter));
    }

    [Fact]
    public void FightStartAndEnd_LeaveOutAndDown_WakeOnlySleepers()
    {
        var down = Hero("DOWN");
        down.Status = CharacterStatus.Down;
        var outMember = Hero("OUT");
        outMember.Status = CharacterStatus.Out;
        var sleeper = Hero("SLEEPER");
        sleeper.Status = CharacterStatus.Sleep;
        var (enc, _, _) = Fight(Manual, [down, outMember, sleeper], [Cat()]);
        Assert.Equal(CharacterStatus.Ok, sleeper.Status); // Prepare: only sleep ends
        Assert.Equal(CharacterStatus.Down, down.Status);
        Assert.Equal(CharacterStatus.Out, outMember.Status);

        sleeper.Status = CharacterStatus.Sleep;
        Assert.Equal(1, CombatEncounter.WakeParty(enc.Party));
        Assert.Equal(CharacterStatus.Down, down.Status);
        Assert.Equal(CharacterStatus.Out, outMember.Status);
    }
}
