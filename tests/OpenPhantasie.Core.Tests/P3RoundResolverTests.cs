using OpenPhantasie.Core;
using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// III's combat round (<see cref="P3RoundResolver"/>, <c>combat-round.md</c>): turn order with bowmen first, the round
/// modes, the shared wake chance, monster specials by cost and the decoded spell table, the round end (give-up, rank swap,
/// compaction with bodies by slot) and the III session using it.
/// </summary>
public class P3RoundResolverTests
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

    /// <summary>Always the same rnd value (clamped into the range asked).</summary>
    private sealed class Always(int rnd) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(rnd, minInclusive, maxExclusive - 1);
    }

    private sealed class NoItems : IItemCatalog
    {
        public ItemCatalogEntry? Get(int itemId) => null;
        public int Count => 0;
    }

    private static readonly P3HitLocationTable Locations = new P3HitLocationTable(
    [
        [69, 80, 84, 91, 91, 100],
        [17, 33, 49, 66, 95, 100],
        [3, 11, 35, 49, 80, 100],
        [40, 57, 66, 80, 89, 100],
        [0, 5, 22, 52, 70, 100],
    ]).Validate();

    private static Character Hero(string name, int hp = 50, int magic = 0) => new()
    {
        Name = name,
        Class = CharacterClass.Fighter,
        Stats = new CharacterStats(17, 10, 10, 17, 10, 10),
        Skills = new CharacterSkills(120, 40, 0, 0, 0, 0, 0, 0),
        HpCur = hp,
        HpMax = hp,
        MagicCur = magic,
        MagicMax = magic,
        BodyCondition = new P3BodyAreas().ToCondition(),
    };

    private static Monster Mon(string name = "Orc", int hp = 9, int level = 1, int magic = 0, int[]? specials = null,
        int specialChance = 0) => new()
    {
        Id = 5, Name = name, HitPoints = hp, Level = level, CombatRating = magic, AttackPower = 40, DamageField = 7,
        Defense = 0, Specials = specials ?? [0, 0, 0], SpecialChance = specialChance,
    };

    private static (CombatEncounter Enc, P3Fight Fight) Fight(Character[] members, params Monster[][] groups)
    {
        var party = new Party(RulesProfile.PhantasieI) { Death = Phantasie3DeathRule.Instance };
        party.Members.AddRange(members);
        var fight = new P3Fight { HitLocations = Locations, Items = new NoItems() };
        var enc = new CombatEncounter(party)
        {
            Rules = GameOptions.ForPreset(GameId.P3), Modules = FeatureModuleSet.ForGame(GameId.P3), CampaignState = fight,
        };
        foreach (var g in groups)
            enc.AddRank(g);
        enc.Prepare();
        fight.StartStrength = P3RoundResolver.Strength(enc.Ranks);
        return (enc, fight);
    }

    // ---- Turn order ----------------------------------------------------------------------------------------------------

    [Fact]
    public void TurnOrder_BowmenFirstInPartyOrder_RestReversed_WhenEverySwapIsANoOp()
    {
        // Party of 4, members 1 and 3 fire bows: order = 1, 3, then slots 2..16 = (empty 2..), 2, 0 reversed from the back.
        // With rnd always 1 every swap exchanges slot k with itself.
        var order = P3RoundResolver.TurnOrder(P3RoundMode.Normal, 4, i => i is 1 or 3, new Always(1));
        Assert.Equal([1, 3, 2, 0, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16], order);
    }

    [Fact]
    public void TurnOrder_Modes_LeaveOneSideOut_AndSwapCountIs17MinusK()
    {
        var monstersOnly = new Script([.. Enumerable.Repeat(1, 2 * 10)]); // k = 7: 10 swaps of two rolls
        var m = P3RoundResolver.TurnOrder(P3RoundMode.MonstersOnly, 4, _ => false, monstersOnly);
        Assert.True(monstersOnly.Done);
        Assert.All(m, a => Assert.InRange(a, 7, 16));
        Assert.Equal(10, m.Count);

        var partyOnly = new Script([.. Enumerable.Repeat(1, 2 * 16)]); // one bowman: k = 1, 16 swaps
        var p = P3RoundResolver.TurnOrder(P3RoundMode.PartyOnly, 3, i => i == 2, partyOnly);
        Assert.True(partyOnly.Done);
        Assert.Equal([2, 1, 0], p);
    }

    [Fact]
    public void TurnOrder_SwapsMixTheNonBowmen()
    {
        // No bowmen, party of 2 (slots 1, 0); first swap exchanges slot 0 and slot 16 (rnd 1 and rnd 17), rest no-ops.
        var rolls = new List<int> { 1, 17 };
        rolls.AddRange(Enumerable.Repeat(1, 2 * 16));
        var order = P3RoundResolver.TurnOrder(P3RoundMode.Normal, 2, _ => false, new Script([.. rolls]));
        Assert.Equal(16, order[0]);
        Assert.Equal(1, order[^1]);
    }

    // ---- Specials ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    [InlineData(32, 0)]
    [InlineData(33, 2)]
    [InlineData(37, 2)]
    public void SpecialCost_IsCodeMod4_Then2(int code, int cost) => Assert.Equal(cost, P3RoundResolver.SpecialCost(code));

    [Theory]
    [InlineData(37, 36)] // code 37 rolls rnd(36): a scripted 36 is out of range for rnd(20)
    [InlineData(36, 20)] // code 36 rolls rnd(20)
    public void Breath_HitsEveryLivingMember_ArmourSoaks_ResistDeals1(int code, int roll)
    {
        var a = Hero("A", magic: 0);
        var b = Hero("B", magic: 50); // 50 + 5·(4 − 50) < 1: always resists
        var dragon = Mon("Dragon", magic: 4, specials: [code, code, code], specialChance: 100);
        var (enc, fight) = Fight([a, b], [dragon]);
        fight.SetArmour(a, 3);
        fight.NextRound = P3RoundMode.MonstersOnly; // the dragon's turn alone
        var rolls = new List<int>();
        rolls.AddRange(Enumerable.Repeat(1, 2 * 10));          // 10 no-op swaps: order 7, 8, …
        rolls.Add(1);                                         // special chance: rnd(100) = 1 ≤ 100
        rolls.Add(1);                                         // which special: rnd(3) = 1
        rolls.Add(10);                                        // A resists? rnd(100) = 10 ≤ 50 + 20: no
        rolls.Add(roll);                                      // A: the breath die − 3 armour
        rolls.Add(1);                                         // B resists? 1 > 50 + 5·(4 − 50) = −180: yes → 1
        rolls.Add(1);                                         // round end: give-up roll (P3Encounters.Surrenders)
        var rng = new FallbackScript(rolls);
        var result = new P3RoundResolver().ResolveRound(enc, rng);
        Assert.False(result.EncounterOver);
        Assert.Equal(50 - (roll - 3), a.HpCur);
        Assert.Equal(50 - 1, b.HpCur);
        Assert.Equal(4 - 2, dragon.CombatRating); // breath costs 2
        Assert.Contains(GameText.F("core.p3.x_breathes", "Dragon"), result.Log);
    }

    [OriginalDataFact]
    public void Special_TooExpensive_FallsBackToMelee_FreeCodesCostNothing()
    {
        var a = Hero("A");
        var mage = Mon("Mage", magic: 0, specials: [3, 3, 3], specialChance: 100); // Fireflash 3 costs 3
        var (enc, fight) = Fight([a], [mage]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        Assert.DoesNotContain(GameText.F("core.p3.x_casts_fireflash", "Mage"), result.Log);
        Assert.Contains(result.Log, l => l == GameText.F("core.combat.x_hits", "Mage")
                                         || l == GameText.F("core.combat.x_misses", "Mage"));

        var b = Hero("B");
        var priest = Mon("Priest", magic: 0, specials: [4, 4, 4], specialChance: 100); // Fireflash 4 is free
        var (enc2, fight2) = Fight([b], [priest]);
        fight2.NextRound = P3RoundMode.MonstersOnly;
        var r2 = new P3RoundResolver().ResolveRound(enc2, new Always(1));
        Assert.Contains(GameText.F("core.p3.x_casts_fireflash", "Priest"), r2.Log);
        Assert.Equal(50 - 1, b.HpCur); // rnd(120) = 1, no injury
        Assert.True(P3BodyAreas.Of(b).AllOkay);
    }

    [Fact]
    public void Quickness_SetsBothGroups_ArmourReduced_ScalesFightArmour()
    {
        var a = Hero("A");
        a.WornArmor = 0;
        var caster = Mon("Shaman", magic: 0, specials: [8, 8, 8], specialChance: 100); // Quickness 8: free, attack 280
        var other = Mon("Ogre");
        var (enc, fight) = Fight([a], [caster], [other]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        new P3RoundResolver().ResolveRound(enc, new Always(1));
        Assert.Equal(280, caster.AttackPower);
        Assert.Equal(280, other.AttackPower);

        var b = Hero("B");
        var witch = Mon("Witch", magic: 0, specials: [20, 20, 20], specialChance: 100); // armour to 20 %, free
        var (enc2, fight2) = Fight([b], [witch]);
        fight2.SetArmour(b, 10);
        fight2.NextRound = P3RoundMode.MonstersOnly;
        var r2 = new P3RoundResolver().ResolveRound(enc2, new Always(1)); // resist roll 1 ≤ 50: not resisted
        Assert.Equal(2, fight2.ArmourOf(b));
        Assert.Contains(GameText.S("core.p3.party_armour_reduced"), r2.Log);
    }

    // ---- Hits noted for the front end (s110: III Amiga's spell flash) --------------------------------------------------

    [Fact]
    public void Hits_AMonsterSpell_IsNotedAsASpecialHit_OnTheMember()
    {
        var b = Hero("B");
        var priest = Mon("Priest", magic: 0, specials: [4, 4, 4], specialChance: 100); // Fireflash 4: free
        var (enc, fight) = Fight([b], [priest]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        var hit = Assert.Single(enc.TakeRoundHits());
        Assert.Equal(new CombatHit(DamageSource.MonsterSpecial, b, null, Line: 1), hit);
        Assert.Equal(GameText.F("core.p3.x_casts_fireflash", "Priest"), result.Log[0]); // the cast line, then the flash
        Assert.Contains(enc.TakeRoundLines(), t => t.Line == 0 && t.Sound == P3Fight.CastSound); // sound 21 (0x17184)
        Assert.Equal(GameText.F("core.combat.x_takes_x_damage", "B", 50 - b.HpCur), result.Log[hit.Line]);
        Assert.Empty(enc.TakeRoundHits()); // taken: the next round starts afresh
    }

    [Fact]
    public void Hits_LoweredArmour_IsNotedWithoutDamage_AsTheAmigaFlashesIt()
    {
        var b = Hero("B");
        var witch = Mon("Witch", magic: 0, specials: [20, 20, 20], specialChance: 100);
        var (enc, fight) = Fight([b], [witch]);
        fight.SetArmour(b, 10);
        fight.NextRound = P3RoundMode.MonstersOnly;
        new P3RoundResolver().ResolveRound(enc, new Always(1));
        // Flashed before the round's one line ("armour reduced"): Weaken notes its hits, then prints.
        Assert.Equal([new CombatHit(DamageSource.MonsterSpecial, b, null, Line: 0)], enc.TakeRoundHits());
        Assert.Contains(enc.TakeRoundLines(), t => t.Line == 0 && t.Sound == P3Fight.CastSound); // sound 21 (0x17316)
        Assert.Equal(50, b.HpCur);
    }

    [Fact]
    public void Hits_APlainSwing_IsAMeleeHit_NotATouch()
    {
        var a = Hero("A");
        var orc = Mon("Orc");
        var (enc, fight) = Fight([a], [orc]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        Assert.Contains(GameText.F("core.combat.x_hits", "Orc"), result.Log);
        Assert.All(enc.TakeRoundHits(), h => Assert.Equal(new CombatHit(DamageSource.MonsterMelee, a, null, Line: 1), h));
    }

    [Fact]
    public void LineTags_AMonsterTurn_OpensWithTheMonster_AndItsSwingIsTagged()
    {
        var a = Hero("A");
        var orc = Mon("Orc");
        var (enc, fight) = Fight([a], [orc]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        var tags = enc.TakeRoundLines();
        Assert.Equal(new CombatLineTag(0, null, orc), tags[0]);                  // the turn, before any line
        var swing = Assert.Single(tags, t => t.Swing != CombatSwing.None);
        Assert.Same(orc, swing.Monster);
        Assert.Equal(CombatSwing.Hit, swing.Swing);
        Assert.DoesNotContain(tags, t => t.Sound != 0); // a swing plays 15 / 18, not the cast sound
        Assert.Equal(GameText.F("core.combat.x_hits", "Orc"), result.Log[swing.Line]);
        Assert.Empty(enc.TakeRoundLines());
    }

    /// <summary>s158 (Amiga <c>0x15990</c>, FORMAT.md 7.2): the driver's stop 33 comes before "The monsters flee!".</summary>
    [Fact]
    public void LineTags_MonstersFleeing_TagTheLineWithTheStop()
    {
        var a = Hero("A");
        var orc = Mon("Orc");
        var (enc, fight) = Fight([a], [orc]);
        fight.StartStrength = 100; // far below half: the give-up roll decides
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        Assert.True(result.EncounterOver);
        var line = result.Log.ToList().IndexOf(GameText.S("core.p3.monsters_flee"));
        Assert.True(line >= 0);
        Assert.Contains(enc.TakeRoundLines(), t => t.Line == line && t.Sound == P3Sounds.Stop);
    }

    /// <summary>s156 (FORMAT.md 8.1, <c>0x16230</c>): a bow shot is a swing on III Amiga's field: the attack frame, 15 / 18.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void LineTags_ABowShot_IsTaggedAsASwing(int roll)
    {
        var a = Hero("A");
        var (enc, _) = Fight([a], [Mon(hp: 999)]);
        enc.SetAction(0, P3CombatModule.Pack(new P3ActionChoice(P3Action.FireBow, 1)));
        var result = new P3RoundResolver().ResolveRound(enc, new Always(roll));
        var shot = Assert.Single(enc.TakeRoundLines(), t => ReferenceEquals(t.Member, a) && t.Swing != CombatSwing.None);
        Assert.Equal(GameText.F(shot.Swing == CombatSwing.Hit ? "core.combat.x_hits" : "core.combat.x_misses", "A"),
            result.Log[shot.Line]);
    }

    // ---- Sleep and waking ----------------------------------------------------------------------------------------------

    [Fact]
    public void WakeChance_Starts15_Climbs15PerRound_CapsAt100_AndASleeperWakesOnItsTurn()
    {
        var a = Hero("A");
        var (enc, fight) = Fight([a], [Mon(hp: 999)]);
        a.Status = CharacterStatus.Sleep; // after the fight-start prep, which wakes everybody
        fight.NextRound = P3RoundMode.PartyOnly;
        Assert.Equal(15, fight.WakeChance);
        new P3RoundResolver().ResolveRound(enc, new Always(100)); // rnd(100) = 100 > 15: stays asleep
        Assert.Equal(CharacterStatus.Sleep, a.Status);
        Assert.Equal(30, fight.WakeChance);
        for (var r = 0; r < 10; r++)
        {
            fight.NextRound = P3RoundMode.PartyOnly;
            new P3RoundResolver().ResolveRound(enc, new Always(100));
        }
        Assert.Equal(100, fight.WakeChance);
        Assert.Equal(CharacterStatus.Ok, a.Status); // at 100 the roll of 100 wakes
    }

    [Fact]
    public void Awaken_WakesTheSleepersAtTheEndOfTheRound_NotOnTheCast()
    {
        var a = Hero("A");
        var b = Hero("B", magic: 20);
        var (enc, fight) = Fight([a, b], [Mon(hp: 999)]);
        a.Status = CharacterStatus.Sleep;
        b.Class = CharacterClass.Priest;
        b.SpellsKnown[SpellId.Awaken] = 1;
        Assert.True(SpellCaster.ResolveCombatCast(enc, 1, CombatAction.CastSpell(SpellId.Awaken), new Always(1), []).Success);
        Assert.Equal(CharacterStatus.Sleep, a.Status); // 0xca27 only sets FAR:0xb5c0
        Assert.True(fight.AwakenAtRoundEnd);

        fight.NextRound = P3RoundMode.PartyOnly;
        var log = new P3RoundResolver().ResolveRound(enc, new Always(100)).Log; // the wake roll fails on A's turn
        Assert.Equal(CharacterStatus.Ok, a.Status); // 0xdd85
        Assert.Contains(log, l => l.Contains('A'));
        Assert.False(fight.AwakenAtRoundEnd);
    }

    // ---- Round end -----------------------------------------------------------------------------------------------------

    [Fact]
    public void RoundEnd_EmptyRank1_LetsRank2MoveUp_AndCompactionKeepsBodiesBySlot()
    {
        var a = Hero("A");
        var dead = Mon("Goblin", hp: 0);
        var r2a = Mon("Wolf", hp: 0);
        var r2b = Mon("Wolf", hp: 8);
        var (enc, fight) = Fight([a], [dead], [r2a, r2b]);
        var slot0Body = fight.BodyOf(r2a);
        var result = new P3RoundResolver().ResolveRound(enc, new Always(100)); // give-up: no
        Assert.False(result.EncounterOver);
        var front = enc.Ranks.Single(r => r.RankIndex == 0);
        Assert.Same(r2b, front.Members[0]); // rank 2 moved up, then the living wolf to slot 0
        Assert.Same(slot0Body, fight.BodyOf(r2b)); // with slot 0's body
        Assert.Same(dead, enc.Ranks.Single(r => r.RankIndex == 1).Members[0]);
    }

    [Fact]
    public void Compaction_AMoverKeepsItsStatusUntilItsNextHit()
    {
        var a = Hero("A");
        var dead = Mon("Wolf", hp: 0);
        var mover = Mon("Wolf", hp: 9);
        var (enc, fight) = Fight([a], [dead, mover]);
        fight.BodyOf(dead)[P3BodyArea.Head] = P3AreaState.Broken; // slot 0's body: an Out wolf died there
        new P3RoundResolver().ResolveRound(enc, new Always(100));
        Assert.Same(mover, enc.Ranks[0].Members[0]);
        Assert.Equal(P3AreaState.Broken, fight.BodyOf(mover)[P3BodyArea.Head]); // slot 0's body now
        Assert.Equal(P3Status.Okay, fight.StatusOf(mover));                    // but the status moved with it
        fight.RefreshStatus(mover);                                             // a hit recomputes it
        Assert.Equal(P3Status.Out, fight.StatusOf(mover));
    }

    [Fact]
    public void RoundEnd_PartyWipe_IgnoresTheElemental()
    {
        var a = Hero("A");
        a.Status = CharacterStatus.Dead;
        a.HpCur = 0;
        var elemental = Hero("Elemental");
        var (enc, _) = Fight([a], [Mon(hp: 999)]);
        enc.Party.Members.Add(elemental);
        enc.Party.Elemental = elemental;
        enc.Prepare(fightStart: false);
        var result = new P3RoundResolver().ResolveRound(enc, new Always(100));
        Assert.True(result.PartyWiped);
    }

    [Fact]
    public void RoundEnd_AllMonstersDead_IsAWin()
    {
        var (enc, _) = Fight([Hero("A")], [Mon(hp: 0)]);
        var result = new P3RoundResolver().ResolveRound(enc, new Always(100));
        Assert.True(result.PartyWon);
        Assert.True(result.EncounterOver);
    }

    [Fact]
    public void ActionsCarryOver_BetweenRounds()
    {
        var a = Hero("A");
        var (enc, _) = Fight([a], [Mon(hp: 999)]);
        var thrust = P3CombatModule.Pack(new P3ActionChoice(P3Action.Thrust, 1));
        enc.SetAction(0, thrust);
        new P3RoundResolver().ResolveRound(enc, new Always(100));
        Assert.Equal(thrust, enc.Prep[0].PackedAction);
    }

    // ---- In a III session ----------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Session_UsesIIIsRound_AndAFailedFleeGivesTheMonstersTheNextRound()
    {
        var s = WithParty();
        s.LeaveTown();
        s.BeginWildernessCombat(Phantasie3Campaign.LordWood); // Flee always fails against a final boss
        var enc = s.Combat!;
        var fight = Assert.IsType<P3Fight>(enc.CampaignState);
        var orders = enc.Prep.Select(p => p.PackedAction).ToArray();
        Assert.Equal(PartyOptionOutcome.LoseRound, s.ChoosePartyOption(PartyOption.Flee).Outcome);
        Assert.Equal(P3RoundMode.MonstersOnly, fight.NextRound);
        Assert.Equal(orders, enc.Prep.Select(p => p.PackedAction).ToArray()); // the actions stay
        s.ResolveCombatRound();
        if (s.Combat is not null)
        {
            Assert.Equal(P3RoundMode.Normal, fight.NextRound);
            Assert.Equal(30, fight.WakeChance);
        }
        Assert.Equal(MoraleOutcome.Stand, Campaign(s).MoraleCheck(s, enc, []));
    }

    [OriginalDataFact]
    public void Session_DosParry_TheFightsLastOrdersAreSaved()
    {
        var s = WithParty(options: GameOptions.ForPreset(GameId.P3, OptionPreset.Dos));
        s.LeaveTown();
        var enc = s.BeginWildernessCombat(0); // a Skeleton
        var attack = P3CombatModule.Pack(new P3ActionChoice(P3Action.Attack));
        CombatResult? last = null;
        for (var r = 0; r < 100 && s.Combat is not null; r++)
        {
            enc.Prep[0].PackedAction = CombatAction.Parry;
            for (var i = 1; i < enc.Prep.Length; i++)
                enc.Prep[i].PackedAction = attack;
            last = s.ResolveCombatRound();
        }
        Assert.NotNull(last);
        Assert.True(last!.EncounterOver);
        Assert.False(last.PartyWiped);
        Assert.True(s.Party.Members[0].P3.ParrySaved); // +0x6e = Parry at fight end (0xd4ff)
        Assert.All(s.Party.Members.Skip(1), m => Assert.False(m.P3.ParrySaved));
    }

    // ---- s137: sound 39 on a III monster's " dies!" line (damage_monster 0x1833e; a member's death has none) ---------------

    [Theory]
    [InlineData(4)]  // Fireflash
    [InlineData(28)] // Mindblast
    [InlineData(36)] // breath
    public void Dies_AMemberKilledByAMonsterSpell_HasNoSound39(int special)
    {
        var b = Hero("B", hp: 1);
        var caster = Mon("Priest", magic: 0, specials: [special, special, special], specialChance: 100);
        var (enc, fight) = Fight([b], [caster]);
        fight.NextRound = P3RoundMode.MonstersOnly;
        var result = new P3RoundResolver().ResolveRound(enc, new Always(1));
        Assert.Equal(CharacterStatus.Dead, b.Status);
        Assert.Contains(GameText.F("core.combat.x_dies", "B"), result.Log); // damage_character, not damage_monster
        Assert.DoesNotContain(enc.TakeRoundLines(), t => t.Sound == P3Sounds.Dies);
    }

    [Fact]
    public void Dies_AMonsterKilledByAMeleeHit_PlaysSound39()
    {
        var b = Hero("B");
        var mon = Mon("Orc", hp: 3);
        var (enc, fight) = Fight([b], [mon]);
        var log = new List<string>();
        P3CombatModule.HitMonster(enc, fight, mon, 5, P3LocationRow.Melee, new Always(1), log);
        var line = log.IndexOf(GameText.F("core.combat.x_dies", "Orc"));
        Assert.True(line >= 0);
        Assert.Contains(enc.TakeRoundLines(), t => t.Line == line && t.Sound == P3Sounds.Dies);
    }

    /// <summary>A script that answers 1 once it runs out (the tail of a round need not be spelled out).</summary>
    private sealed class FallbackScript(IReadOnlyList<int> values) : IRandomSource
    {
        private int _i;

        public int Next(int minInclusive, int maxExclusive)
        {
            var v = _i < values.Count ? values[_i++] : minInclusive;
            Assert.InRange(v, minInclusive, maxExclusive - 1);
            return v;
        }
    }
}
