using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// PLAN P3.2 Authentic mode, the survey tranche: the DECIDED manual-side D-entries whose DOS side is one contained switch,
/// as <c>manual</c> / <c>dos</c> rules options. Each test pins both values; the manual value is today's rule.
/// </summary>
public class AuthenticModeSwitchesTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P1);

    private static GameOptions Dos(RuleOptionDescriptor d) => Manual.With(d.Id, "dos");

    /// <summary>Replays queued values (clamped to the range), then repeats the last one.</summary>
    private sealed class Scripted(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);
        private int _last;

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_q.Count > 0)
                _last = _q.Dequeue();
            return Math.Clamp(_last, minInclusive, maxExclusive - 1);
        }

        public int NextDosWord() => Next(0, 32768);
    }

    private static Character Caster(int knownSpell, int level = 1) => new()
    {
        Name = "MAGE",
        Class = CharacterClass.Wizard,
        Status = CharacterStatus.Ok,
        Level = level,
        Stats = new CharacterStats(10, 18, 10, 10, 10, 10),
        HpCur = 20, HpMax = 20,
        MagicCur = 10, MagicMax = 10, MpCeiling = 10,
        SpellsKnown = Enumerable.Range(0, SpellId.Count).Select(i => i == knownSpell ? 1 : 0).ToArray(),
    };

    private static CombatEncounter Fight(GameOptions rules, Character caster, params Monster[][] ranks)
    {
        var party = new Party();
        party.Members.Add(caster);
        var enc = new CombatEncounter(party) { Rules = rules };
        foreach (var r in ranks)
            enc.AddRank(r);
        enc.Prepare();
        return enc;
    }

    private static Monster Mon(string name, int defense = 0, int cv = 10, int level = 1, long xp = 10, bool undead = false) =>
        new() { Name = name, HitPoints = 50, CombatRating = cv, Defense = defense, Level = level, ExperienceValue = xp, Undead = undead };

    // ---- registry -------------------------------------------------------------------------------------------------

    public static TheoryData<string, string> Switches => new()
    {
        { "p1.attack-gate", "D-011" }, { "p1.lunge", "D-012" }, { "p1.binding", "D-013" }, { "p1.confusion-iv", "D-014" },
        { "p1.fear", "D-021" }, { "p1.dispell-undead", "D-024" }, { "p1.spell-level", "D-028" },
        { "p1.fireflash-damage", "D-029" }, { "p1.fireflash-target", "D-059" }, { "p1.failed-run", "D-030" },
        { "p1.town-inn-time", "D-004" }, { "p1.hit-dice", "D-007" }, { "p1.wild-inn-time", "D-008" },
        { "p1.inn-magic-cap", "D-046" }, { "p1.wild-inn-dead", "D-047" }, { "p1.pool-cap", "D-051" },
        { "p1.path-time", "D-055" }, { "p1.monster-eval", "D-056" }, { "p1.vision", "D-057" }, { "p1.add-aging", "D-067" },
        { "p1.cast-flag", "D-070" }, { "p1.dungeon-map-no", "D-027" },
    };

    [Theory]
    [MemberData(nameof(Switches))]
    public void Switch_IsManualOrDos_OnlyTheDosPresetPlaysDos(string id, string difference)
    {
        var d = RuleOptionRegistry.For(GameId.P1).Single(o => o.Id == id);
        Assert.Equal(["manual", "dos"], d.Values);
        Assert.Equal(("manual", "dos", "manual", difference), (d.Manual, d.Dos, d.Remaster, d.Difference));
        Assert.False(GameOptions.ForPreset(GameId.P1).IsDos(d));
        Assert.True(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).IsDos(d));
        Assert.False(GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster).IsDos(d));
    }

    // ---- combat orders (D-011, D-012) -----------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(CombatChoice.Attack, 70, false, 20)]  // manual: ATTACK always 2 swings
    [InlineData(CombatChoice.Attack, 70, true, 10)]   // DOS: THRUST below 75
    [InlineData(CombatChoice.Attack, 75, true, 20)]
    [InlineData(CombatChoice.Slash, 70, false, 30)]   // manual: SLASH 3
    [InlineData(CombatChoice.Slash, 70, true, 10)]    // DOS: SLASH → ATTACK → THRUST
    [InlineData(CombatChoice.Slash, 149, true, 20)]   // DOS: ATTACK below 150
    [InlineData(CombatChoice.Slash, 150, true, 30)]   // 150/75 + 1 = 3
    [InlineData(CombatChoice.Slash, 225, true, 40)]
    [InlineData(CombatChoice.Slash, 230, false, 40)]
    public void AttackAndSlash_SkillGate_D011(CombatChoice choice, int weaponHit, bool dos, int packed)
    {
        var ch = new Character { Class = CharacterClass.Fighter, Status = CharacterStatus.Ok };
        var prep = new CombatantPrep { WeaponHitEff = weaponHit };
        var rules = dos ? Dos(RuleOptionRegistry.AttackGate) : Manual;
        Assert.Equal(packed, CombatOrders.Choose(ch, prep, choice, new Scripted(0), rules: rules));
    }

    [OriginalDataTheory]
    [InlineData(CharacterClass.Thief, false, 10)] // manual: a rank-1 THRUST
    [InlineData(CharacterClass.Thief, true, 11)]  // DOS: thieves LUNGE too
    [InlineData(CharacterClass.Fighter, false, 11)]
    [InlineData(CharacterClass.Priest, true, 10)]
    public void Lunge_ForThieves_D012(CharacterClass cls, bool dos, int packed)
    {
        var ch = new Character { Class = cls, Status = CharacterStatus.Ok };
        var rules = dos ? Dos(RuleOptionRegistry.Lunge) : Manual;
        Assert.Equal(packed, CombatOrders.Choose(ch, new CombatantPrep(), CombatChoice.Lunge, new Scripted(0), rules: rules));
    }

    [OriginalDataFact]
    public void Session_ChooseAction_PassesTheSavesRules_D012()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(Dos(RuleOptionRegistry.Lunge));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Sneak", Race.Human, CharacterClass.Thief), out _));
        s.DepartGuildToWilderness();
        s.BeginWildernessCombat(Data.Monsters.First(m => m.HpBase <= 8).Id);
        Assert.Equal(CombatAction.Lunge(), s.ChooseAction(0, CombatChoice.Lunge));
    }

    // ---- spells (D-013, D-014, D-028, D-029, D-059, D-021, D-024) -------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, -86)] // manual: −75 − max(75·15/100, 3)
    [InlineData(true, -63)]  // DOS: −75·85/100, harder to hit
    public void Binding_D013(bool dos, int defense)
    {
        var skeleton = Mon("SKELETON", defense: -75);
        var enc = Fight(dos ? Dos(RuleOptionRegistry.Binding) : Manual, Caster(SpellId.BindingI), [skeleton]);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.BindingI), new Scripted(0), []);
        Assert.Equal(defense, skeleton.Defense);
    }

    [OriginalDataTheory]
    [InlineData(false, 0)]
    [InlineData(true, 5)]
    public void ConfusionIv_D014(bool dos, int gate)
    {
        var mon = Mon("WIZARD");
        mon.SpecialChance = 100;
        var enc = Fight(dos ? Dos(RuleOptionRegistry.ConfusionIv) : Manual, Caster(SpellId.ConfusionIv), [mon]);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.ConfusionIv), new Scripted(0), []);
        Assert.Equal(gate, mon.SpecialChance);
    }

    [OriginalDataTheory]
    [InlineData(false, 80)] // manual: 50 + 2·(10 − 1) = 68 ≥ roll 60 → hit, attack 100 → 80
    [InlineData(true, 100)] // DOS: 50 < 60 → miss
    public void SpellRoll_LevelTerm_D028(bool dos, int attack)
    {
        var mon = Mon("ORC", cv: 10, level: 1);
        mon.AttackPower = 100;
        var caster = Caster(SpellId.WeaknessI, level: 10); // MP max 10 = CV: base chance 50
        var enc = Fight(dos ? Dos(RuleOptionRegistry.SpellLevel) : Manual, caster, [mon]);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.WeaknessI), new Scripted(60), []);
        Assert.Equal(attack, mon.AttackPower);
    }

    [Theory]
    [InlineData(99, 0, 18, 9)]   // 99·9·18/2000 + 1
    [InlineData(99, 3, 18, 90)]
    [InlineData(0, 3, 18, 1)]
    public void FireflashDamageDos_IsTheTracedFormula_D029(int roll, int tier, int intel, int damage) =>
        Assert.Equal(damage, Magic.FireflashDamageDos(roll, tier, intel));

    [OriginalDataTheory]
    [InlineData(false, false, "FRONT0", 10)] // manual: front rank; 1 + 99·9·18/(99·18) = 10
    [InlineData(true, false, "FRONT0", 9)]   // DOS damage, manual target
    [InlineData(false, true, "BACK0", 10)]   // DOS target: the rearmost group, never its last monster
    [InlineData(true, true, "BACK0", 9)]
    public void Fireflash_DamageAndTarget_D029_D059(bool dosDamage, bool dosTarget, string hit, int damage)
    {
        var rules = Manual;
        if (dosDamage) rules = rules.With(RuleOptionRegistry.FireflashDamage.Id, "dos");
        if (dosTarget) rules = rules.With(RuleOptionRegistry.FireflashTarget.Id, "dos");
        var front = new[] { Mon("FRONT0"), Mon("FRONT1") };
        var back = new[] { Mon("BACK0"), Mon("BACK1") };
        var enc = Fight(rules, Caster(SpellId.FireflashI), front, back);
        // damage roll 99, then the target roll 0 (manual: rand(0, 2) = 0; DOS: 0·L/100 = 0)
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.FireflashI), new Scripted(99, 0), []);
        var target = enc.AllMonsters.Single(m => m.HitPoints < 50);
        Assert.Equal(hit, target.Name);
        Assert.Equal(50 - damage, target.HitPoints);
    }

    [OriginalDataFact]
    public void FireflashTargetDos_NeverTheLastOfAGroup_D059()
    {
        var back = new[] { Mon("B0"), Mon("B1"), Mon("B2") };
        var enc = Fight(Manual, Caster(SpellId.FireflashI), [Mon("F0")], back);
        Assert.Equal("B1", SpellCaster.PickFireflashTargetDos(enc, new Scripted(99))!.Name); // 99·2/100 = 1
        var lone = Fight(Manual, Caster(SpellId.FireflashI), [Mon("F0")], [Mon("LONE")]);
        Assert.Equal("LONE", SpellCaster.PickFireflashTargetDos(lone, new Scripted(50))!.Name);
    }

    [OriginalDataTheory]
    [InlineData(false, SpellCombatEnd.Ended)]
    [InlineData(true, SpellCombatEnd.None)] // DOS: L −= 6 only, the fight goes on
    public void Fear_D021(bool dos, SpellCombatEnd end)
    {
        var enc = Fight(dos ? Dos(RuleOptionRegistry.Fear) : Manual, Caster(SpellId.Fear, level: 5), [Mon("ORC")]);
        enc.MonsterLevel = 5;
        Assert.True(CombatSpells.Fear(enc, enc.Party.Members[0], new Scripted(0), []));
        Assert.Equal(-1, enc.MonsterLevel);
        Assert.Equal(end, enc.SpellEnd);
        Assert.Equal(!dos, enc.MonstersFrozen);
    }

    [Theory]
    [InlineData(false, 14, false, false)]
    [InlineData(true, 14, false, true)]   // DOS: XP divisible by 7 (DEMON, BANTIR …)
    [InlineData(true, 15, true, false)]   // DOS ignores the data flag
    [InlineData(false, 15, true, true)]
    public void DispellUndead_WhatCountsAsUndead_D024(bool dos, long xp, bool flag, bool undead) =>
        Assert.Equal(undead, CombatSpells.IsUndead(Mon("X", xp: xp, undead: flag), dos));

    // ---- failed monster run (D-030) -------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void FailedRun_WakesSleepers_OnlyUnderDos_D030(bool dos, bool stillAsleep)
    {
        var mon = Mon("ORC");
        mon.StatusFlags = MonsterStatusFlags.Asleep;
        var enc = Fight(dos ? Dos(RuleOptionRegistry.FailedRun) : Manual, Caster(SpellId.Fear), [mon]);
        enc.RewardXp = 10_000;
        Assert.Equal(MoraleOutcome.RunFailed, PartyOptions.MoraleCheck(enc, new Scripted(0, 0, 99), 0));
        Assert.Equal(stillAsleep, (mon.StatusFlags & MonsterStatusFlags.Asleep) != 0);
    }

    // ---- town and Guild (D-004, D-007, D-067) ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, 501)]
    [InlineData(true, 500)]
    public void TownInn_StayTime_D004(bool dos, int ageClock)
    {
        var m = new Character { Name = "A", Status = CharacterStatus.Ok, HpMax = 10, AgeClock = 500 };
        var party = new Party();
        party.Members.Add(m);
        TownMenus.Rest(party, new Scripted(0), dosNoTime: dos);
        Assert.Equal(ageClock, m.AgeClock);
        Assert.Equal(10, m.HpCur);
    }

    [Theory]
    [InlineData(10, 10, false, 10)] // 10·15/15
    [InlineData(10, 10, true, 9)]   // 150 >> 4
    [InlineData(16, 18, true, 23)]  // 368 >> 4
    [InlineData(16, 18, false, 24)] // 368 / 15
    public void HitDice_D007(int factor, int con, bool dos, int dice) =>
        Assert.Equal(dice, LevelUp.HitDiceCount(factor, con, dos));

    [OriginalDataFact]
    public void HitDice_ReachCreationAndTraining_D007()
    {
        int StartHp(GameOptions o) => Enumerable.Range(1, 20).Sum(seed =>
        {
            var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
            s.StartNewGame(o);
            s.OpenGuild();
            return s.CreateAndEnroll("Tank", Race.Human, CharacterClass.Fighter).HpMax;
        });
        Assert.True(StartHp(Manual) > StartHp(Dos(RuleOptionRegistry.HitDice))); // fewer dice under DOS
    }

    [OriginalDataTheory]
    [InlineData(false, 500)]
    [InlineData(true, 504)] // DOS: every Add ages 2 months, same town too
    public void GuildAdd_Aging_D067(bool dos, int ageClock)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(dos ? Dos(RuleOptionRegistry.AddAging) : Manual);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        hero.AgeClock = 500;
        Assert.True(s.TryAddToParty(hero, out _));
        Assert.Equal(ageClock, hero.AgeClock);
    }

    // ---- wilderness inn (D-008, D-046, D-047) ---------------------------------------------------------------------

    private static (Party Party, Character Caster, Character Dead) InnParty()
    {
        var caster = new Character
        {
            Name = "C", Status = CharacterStatus.Ok, HpMax = 20, HpCur = 1, MagicMax = 10, MagicCur = 1, MpCeiling = 10,
            CastSinceRest = true, AgeClock = 500,
        };
        var dead = new Character { Name = "D", Status = CharacterStatus.Dead, HpMax = 20, MagicMax = 5, AgeClock = 500 };
        var party = new Party { GoldCarried = 300 };
        party.Members.AddRange([caster, dead]);
        return (party, caster, dead);
    }

    [OriginalDataTheory]
    [InlineData(false, 10)] // gap 0, roll 0: < never passes
    [InlineData(true, 11)]  // DOS <=: one past the table
    public void WildernessInn_MagicRaise_D046(bool dos, int magicMax)
    {
        var (party, caster, _) = InnParty();
        WildernessInn.Stay(party, new LootSack(), new Scripted(0), dos ? Dos(RuleOptionRegistry.InnMagicCap) : Manual);
        Assert.Equal(magicMax, caster.MagicMax);
    }

    [OriginalDataTheory]
    [InlineData(false, 20)]
    [InlineData(true, 0)] // DOS skips the dead
    public void WildernessInn_Dead_D047(bool dos, int hp)
    {
        var (party, _, dead) = InnParty();
        WildernessInn.Stay(party, new LootSack(), new Scripted(99), dos ? Dos(RuleOptionRegistry.WildernessInnDead) : Manual);
        Assert.Equal(hp, dead.HpCur);
        Assert.Equal(CharacterStatus.Dead, dead.Status);
    }

    [OriginalDataTheory]
    [InlineData(false, 501, 0)]
    [InlineData(true, 500, 1000)] // DOS: no age, +1000 travel time (≈ 3.9 DAY OUT)
    public void WildernessInn_StayTime_D008(bool dos, int ageClock, int travel)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(dos ? Dos(RuleOptionRegistry.WildernessInnTime) : Manual);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.DepartGuildToWilderness();
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (1, 7, 13); // OUT1 (7,13), tile 41
        s.Party.GoldCarried = 500;
        hero.AgeClock = 500;
        s.TravelTime = 0;

        Assert.Equal(TownTransactionKind.Ok, s.StayAtWildernessInn().Kind);
        Assert.Equal(ageClock, hero.AgeClock);
        Assert.Equal(travel, s.TravelTime);
    }

    // ---- wilderness (D-051, D-055, D-056, D-057, D-070) -----------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, 18)] // Human STR max 18 (raceMod + 8)
    [InlineData(true, 21)]  // DOS: no cap, +3 (LUCK 13)
    public void MagicalPool_Cap_D051(bool dos, int str)
    {
        var hero = new Character
        {
            Name = "H", Race = Race.Human, BirthRace = Race.Human, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(18, 10, 10, 10, 10, 13), Phantasie1 = { Word90 = 1 },
        };
        var party = new Party();
        party.Members.Add(hero);
        MagicalPool.Apply(party, 1, Data.CreationTables, new Scripted(0), noCap: dos); // roll 0 → STR
        Assert.Equal(str, hero.Stats.Str);
    }

    [OriginalDataTheory]
    [InlineData(false, 30)]
    [InlineData(true, 60)]
    public void PathStep_TravelTime_D055(bool dos, int cost)
    {
        var t = Data.CreateTraversal();
        (int region, int x, int y, int tile) step = default;
        var found = false;
        foreach (var id in OverworldState.ValidRegionIds)
        {
            var r = t.GetRegion(id);
            for (var yy = 0; yy < OverworldRegion.Height && !found; yy++)
            for (var xx = 0; xx + 1 < OverworldRegion.Width && !found; xx++)
                if (r.GetNormalized(xx, yy) / 10 == 6 && r.GetNormalized(xx + 1, yy) / 10 == 6)
                    (step, found) = ((id, xx, yy, r.GetNormalized(xx + 1, yy)), true);
            if (found) break;
        }
        Assert.True(found);

        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(dos ? Dos(RuleOptionRegistry.PathTime) : Manual);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter), out _));
        s.DepartGuildToWilderness();
        (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y) = (step.region, step.x, step.y);
        s.TravelTime = 0;
        s.MoveOverworld(CardinalDirection.East);
        Assert.Equal(cost, s.TravelTime);
    }

    [OriginalDataTheory]
    [InlineData(false, "MONSTERS ARE AT LEVEL 3")] // the nearby pool's average (roll 99: no error)
    [InlineData(true, "MONSTERS ARE AT LEVEL 6")]  // DOS: dungeon 2·tier + 2
    public void MonsterEval_D056(bool dos, string text)
    {
        var caster = Caster(SpellId.MonsterEval);
        var party = new Party();
        party.Members.Add(caster);
        var r = SpellCaster.CastOutOfCombat(party, caster, SpellId.MonsterEval, null, new Scripted(99), CastLocation.Dungeon,
            new FieldCastContext(DungeonTier: 2, NearbyMonsterLevels: [2, 3, 4]), dos ? Dos(RuleOptionRegistry.MonsterEval) : Manual);
        Assert.Equal(text, r.Message);
    }

    [OriginalDataTheory]
    [InlineData(false, 26 * 20)]
    [InlineData(true, 7 * 7)] // DOS: MP 9 left → radius 3, a 7×7 square
    public void Vision_D057(bool dos, int revealed)
    {
        var tiles = Enumerable.Repeat((byte)(8 + OverworldRegion.FogOffset), OverworldRegion.Width * OverworldRegion.Height).ToArray();
        var region = new OverworldRegion(1, "OUT1.DAT", tiles, [], []);
        var caster = Caster(SpellId.Vision);
        var party = new Party();
        party.Members.Add(caster);
        SpellCaster.CastOutOfCombat(party, caster, SpellId.Vision, null, new Scripted(0), CastLocation.Field,
            new FieldCastContext(Region: region, X: 10, Y: 10), dos ? Dos(RuleOptionRegistry.Vision) : Manual);
        Assert.Equal(revealed, region.Tiles.Count(t => t == 8));
    }

    [OriginalDataTheory]
    [InlineData(false, true)]
    [InlineData(true, false)] // DOS: only combat casts set @94
    public void OutOfCombatCast_SetsTheInnFlag_D070(bool dos, bool flag)
    {
        var caster = Caster(SpellId.HealingI);
        var party = new Party();
        party.Members.Add(caster);
        var r = SpellCaster.CastOutOfCombat(party, caster, SpellId.HealingI, caster, new Scripted(0), CastLocation.Town,
            rules: dos ? Dos(RuleOptionRegistry.CastFlag) : Manual);
        Assert.True(r.Success);
        Assert.Equal(flag, caster.CastSinceRest);
    }

    // ---- dungeon map NO (D-027) -----------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, false)]
    [InlineData(true, true)] // DOS: NO writes nothing, the older save stays
    public void DungeonMapNo_D027(bool dos, bool kept)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        s.StartNewGame(dos ? Dos(RuleOptionRegistry.DungeonMapNo) : Manual);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Saver", Race.Human, CharacterClass.Fighter), out _));
        s.DepartGuildToWilderness();
        s.EnterDungeon(1, 20, 0);
        s.DungeonSave = new DungeonSaveSlot(1, new byte[DungeonLevel.BufferSize]);
        s.Dungeon!.Y = 1;
        Assert.Equal(DungeonMoveKind.ExitDungeon, s.MoveDungeon(CardinalDirection.North).Kind);
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal(kept, s.DungeonSave is not null);
    }
}
