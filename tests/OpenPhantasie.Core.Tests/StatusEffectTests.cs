using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>Status effects — apply, tick, expire (<c>combat-resolution.md</c> §Status effects, s31).</summary>
public class StatusEffectTests
{
    private sealed class StrictRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public StrictRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Remaining => _values.Count;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.Equal((0, 100), (minInclusive, maxExclusive));
            Assert.NotEmpty(_values);
            return _values.Dequeue();
        }
    }

    private static Character Member(string name, int attack = 100) => new()
    {
        Name = name,
        Status = CharacterStatus.Ok,
        Class = CharacterClass.Wizard,
        Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        Skills = new CharacterSkills(attack, 0, 0, 0, 0, 0, 0, 0),
        Level = 5,
        HpCur = 20,
        HpMax = 20,
        MagicCur = 20,
        MagicMax = 20,
        SpellsKnown = new int[60],
    };

    private static Monster Mon(string name) => new() { Name = name, HitPoints = 30, AttackPower = 100, DamageField = 0 };

    [Fact]
    public void WakeParty_WakesSleepersLeavesDeadDead()
    {
        var party = new Party();
        var ok = Member("OK");
        var sleeper = Member("NAP");
        sleeper.Status = CharacterStatus.Sleep;
        var dead = Member("DEAD");
        dead.Status = CharacterStatus.Dead;
        party.Members.AddRange(new[] { ok, sleeper, dead });

        Assert.Equal(1, CombatEncounter.WakeParty(party));
        Assert.Equal(CharacterStatus.Ok, ok.Status);
        Assert.Equal(CharacterStatus.Ok, sleeper.Status);
        Assert.Equal(CharacterStatus.Dead, dead.Status);
    }

    [OriginalDataFact]
    public void Session_CombatExit_WakesSleepers()
    {
        // 0x3f29 → 0x3fbb → 0x3eff: nobody leaves a fight asleep (here: BEG, which always ends the fight, D-017).
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Sleepy", Race.Human, CharacterClass.Fighter);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        hero.Status = CharacterStatus.Sleep; // slept mid-fight

        var r = session.ChoosePartyOption(PartyOption.BegMercy);

        Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
        Assert.Equal(CharacterStatus.Ok, hero.Status);
    }

    [Fact]
    public void Prepare_RebuildsEffectiveFields_BuffsLastOneFight()
    {
        var party = new Party();
        var wiz = Member("WIZ", attack: 100);
        party.Members.Add(wiz);
        var enc = new CombatEncounter(party);
        enc.AddRank(Mon("ORC"));
        enc.Prepare();
        enc.Prep[0].WeaponHitEff = 170;  // QUICKNESS IV left over
        enc.Prep[0].ArmorEff = 99;
        wiz.Status = CharacterStatus.Sleep;

        enc.Prepare(); // next fight's prep 0x3263

        Assert.Equal(100, enc.Prep[0].WeaponHitEff);
        Assert.Equal(wiz.ArmorSoak, enc.Prep[0].ArmorEff);
        Assert.Equal(CharacterStatus.Ok, wiz.Status);
    }

    [Fact]
    public void Quickness_RebuildsFromBase_CancelsSlowed()
    {
        // 0x0498: @250 := @30·(100 + pct)/100 — not the current (slowed) value.
        var party = new Party();
        var wiz = Member("WIZ", attack: 100);
        wiz.SpellsKnown[SpellId.QuicknessI] = 1;
        party.Members.Add(wiz);
        var enc = new CombatEncounter(party);
        enc.AddRank(Mon("ORC"));
        enc.Prepare();
        enc.Prep[0].WeaponHitEff = 20; // `core.combat.party_slowed` ×2

        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.QuicknessI),
            new StrictRandomSource(), new List<string>());

        Assert.True(r.Success);
        Assert.Equal(110, enc.Prep[0].WeaponHitEff);
    }

    [Theory]
    // combat-resolution.md §Combat buffs: QUICKNESS @30·(100 + 10/25/45/70)/100, STRENGTH @28 + @240 + 2/4/7/11,
    // PROTECTION @244 + 1/3/6/10 — every tier, each capped at 255.
    [InlineData(SpellId.QuicknessI, 100, 110)]
    [InlineData(SpellId.QuicknessI + 1, 100, 125)]
    [InlineData(SpellId.QuicknessI + 2, 100, 145)]
    [InlineData(SpellId.QuicknessI + 3, 100, 170)]
    [InlineData(SpellId.QuicknessI + 3, 200, 255)]
    [InlineData(SpellId.StrengthI, 10, 12)]
    [InlineData(SpellId.StrengthI + 1, 10, 14)]
    [InlineData(SpellId.StrengthI + 2, 10, 17)]
    [InlineData(SpellId.StrengthI + 3, 10, 21)]
    [InlineData(SpellId.StrengthI + 3, 250, 255)]
    [InlineData(SpellId.ProtectionI, 2, 3)]
    [InlineData(SpellId.ProtectionI + 1, 2, 5)]
    [InlineData(SpellId.ProtectionI + 2, 2, 8)]
    [InlineData(SpellId.ProtectionI + 3, 2, 12)]
    [InlineData(SpellId.ProtectionI + 3, 250, 255)]
    public void PartyBuffs_EveryTier_FromBase_CappedAt255(int spell, int baseValue, int expected)
    {
        var party = new Party();
        var wiz = Member("WIZ", attack: spell < SpellId.StrengthI ? baseValue : 100);
        if (spell >= SpellId.StrengthI && spell < SpellId.ProtectionI)
            wiz.WeaponDamageA = baseValue;
        if (spell >= SpellId.ProtectionI)
            wiz.ArmorSoak = baseValue;
        wiz.SpellsKnown[spell] = 1;
        party.Members.Add(wiz);
        var enc = new CombatEncounter(party);
        enc.AddRank(Mon("ORC"));
        enc.Prepare();

        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(spell), new StrictRandomSource(), new List<string>());

        Assert.True(r.Success);
        var prep = enc.Prep[0];
        Assert.Equal(expected, spell < SpellId.StrengthI ? prep.WeaponHitEff
            : spell < SpellId.ProtectionI ? prep.WeaponDamageEff : prep.ArmorEff);
    }

    [OriginalDataFact]
    public void Sleep_CasterVsGroupCv_GroupFallsAsleep_NoRestack()
    {
        // D-020 (manual): one roll per group, chance = 5·(caster @16 MP max − group CV) + 50, inclusive (OV2 0x13b5, s33).
        // Ceiling @14 = 30 ≠ max: with the old @14 operand group C (CV 20) would get chance 100 and fall asleep.
        var party = new Party();
        var wiz = Member("WIZ");
        wiz.MagicMax = 10;
        wiz.MpCeiling = 30;
        wiz.Level = 0; // same as the monsters' level: no D-028 level term
        wiz.SpellsKnown[SpellId.Sleep] = 1;
        party.Members.Add(wiz);
        var enc = new CombatEncounter(party) { MonsterLevel = 30 }; // L plays no part
        var a = Mon("A");
        var b = Mon("B");
        var c = Mon("C");
        c.CombatRating = 20; // chance 0
        enc.AddRank(a, b);
        enc.AddRank(c);
        enc.Prepare();

        var log = new List<string>();
        var rng = new StrictRandomSource(99, 1);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Sleep), rng, log);

        Assert.Equal(0, rng.Remaining);
        Assert.Equal(MonsterStatusFlags.Asleep, a.StatusFlags);
        Assert.Equal(MonsterStatusFlags.Asleep, b.StatusFlags);
        Assert.Equal(MonsterStatusFlags.None, c.StatusFlags); // 1 > 0: resisted
        Assert.Equal(new[] { "A IS SLEPT", "B IS SLEPT" }, log.Skip(1)); // after "WIZ CASTS A SPELL"
        // s125: each sleeper is a hit (III Amiga 0x181b8 flashes it through damage_monster with 0 damage), before its line.
        var hits = enc.TakeRoundHits();
        Assert.Equal(new[] { (a, 1), (b, 2) }, hits.Select(h => (h.Monster!, h.Line)));
        Assert.All(hits, h => Assert.Equal(DamageSource.PartySpell, h.Source));

        // A second SLEEP doesn't deepen it: the group's sleepers aren't slept again.
        log.Clear();
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Sleep), new StrictRandomSource(0, 99), log);
        Assert.Equal(new[] { "WIZ CASTS A SPELL" }, log); // [D✓] s65: DOS adds no "no effect" line
        Assert.DoesNotContain(log, l => l.EndsWith("IS SLEPT", StringComparison.Ordinal));
    }

    [Fact]
    public void Sleepers_SkipTurns_CanBeHit_StayAsleep()
    {
        // D-020: DOS state 1 — no turns, wake only on rand < 15 per pass (0x1081); still a melee target.
        var party = new Party();
        var hero = Member("HERO", attack: 200);
        hero.WeaponDamageA = 5;
        party.Members.Add(hero);
        var enc = new CombatEncounter(party) { MonsterLevel = 1 };
        var a = Mon("A");
        a.HitPoints = 500;
        a.StatusFlags = MonsterStatusFlags.Asleep;
        enc.AddRank(a);
        enc.Prepare();
        enc.SetAction(0, CombatAction.Thrust(0));

        var round = new CombatResolver().ResolveRound(enc, new StrictRandomSource(Enumerable.Repeat(20, 64).ToArray()));

        Assert.False(round.EncounterOver);
        Assert.True(a.HitPoints < 500);                                       // hit while asleep
        Assert.Equal(MonsterStatusFlags.Asleep, a.StatusFlags);               // 20 ≥ 15 every pass
        Assert.DoesNotContain(round.Log, l => l.StartsWith("A HITS", StringComparison.Ordinal)
                                              || l.StartsWith("A MISSES", StringComparison.Ordinal)
                                              || l.StartsWith("A AWAKENS", StringComparison.Ordinal));
    }

    [Fact]
    public void Sleep_HighEncounterLevel_AlwaysResisted()
    {
        var party = new Party();
        var wiz = Member("WIZ");
        wiz.SpellsKnown[SpellId.Sleep] = 1;
        party.Members.Add(wiz);
        var enc = new CombatEncounter(party) { MonsterLevel = 10 }; // L² = 100 > every roll
        var a = Mon("A");
        enc.AddRank(a);
        enc.Prepare();

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Sleep),
            new StrictRandomSource(0, 99), new List<string>());

        Assert.False(a.DoneThisRound);
    }

    [OriginalDataFact]
    public void Awaken_WakesWholeParty_WokenMemberDoesNotAct()
    {
        // 0x9bf → 0x3eff: one "PARTY AWAKENS"; the sleeper keeps action 4 (no action) this round.
        var party = new Party();
        var priest = Member("PRIEST");
        priest.Class = CharacterClass.Priest;
        priest.SpellsKnown[SpellId.Awaken] = 1;
        var sleeper = Member("DOZER");
        party.Members.Add(priest);
        party.Members.Add(sleeper);
        var enc = new CombatEncounter(party);
        enc.AddRank(Mon("ORC"));
        enc.Prepare();
        sleeper.Status = CharacterStatus.Sleep;
        enc.SetAction(1, CombatAction.Cleared);

        var log = new List<string>();
        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Awaken), new StrictRandomSource(), log);

        Assert.True(r.Success);
        Assert.Equal(CharacterStatus.Ok, sleeper.Status);
        Assert.Equal(new[] { "PARTY AWAKENS" }, log.Where(l => l.Contains("AWAKEN")).ToArray());
        Assert.Equal(CombatAction.Cleared, enc.Prep[1].PackedAction);
    }

    [Fact]
    public void DungeonTrapDeath_LowersCon_LikeCombatDeath()
    {
        // DOS trap / event / swim damage (OV2 0x314f, 0x2642, 0x68ff) calls 0x124a, so a death runs 0x12cf (D-009).
        var hero = Member("TRAPPED");
        hero.Skills = hero.Skills with { SpotTrap = 0, DisarmTrap = 0 };
        hero.HpCur = 1;
        var party = new Party();
        party.Members.Add(hero);
        var cells = new byte[DungeonLevel.GridByteLength];
        cells[0] = 1;
        cells[1] = 3; // trap kind 3: seed 99 neither spots nor avoids it; any hit kills at 1 HP (0x124a floor 1)
        var traversal = new DungeonTraversal([new DungeonLevel(99, "TEST", cells)]);

        var result = traversal.TryMove(new DungeonState { LevelId = 99, X = 0, Y = 0 }, CardinalDirection.East, party,
            new SeededDosRandomSource(99));

        Assert.Equal(DungeonMoveKind.TriggerTrap, result.Kind);
        Assert.Equal(CharacterStatus.Dead, hero.Status);
        Assert.Equal(0, hero.HpCur);
        Assert.Equal(8, hero.Stats.Con); // CON 10, Luck 10 < 11 → −2
    }

    [Theory]
    [InlineData(85, 3, 45)]   // remaster /15 (D-007): 85·8/15
    [InlineData(85, 18, 130)] // 85·23/15
    [InlineData(16, 4, 9)]    // 16·9/15
    public void HitDice_ScaleWithConPlusFive(int factor, int con, int dice) =>
        Assert.Equal(dice, LevelUp.HitDiceCount(factor, con));

    private sealed class Words(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _values = new(values);
        public int Remaining => _values.Count;
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("d3 uses the raw word");
        public int NextDosWord() => _values.Dequeue();
    }

    [Fact]
    public void RollHitDice_EachD3IsWordOver10923PlusOne()
    {
        // factor 16, CON 4 → 9 dice; word bands 0..10922 → 1, 10923..21845 → 2, 21846..32767 → 3.
        var rng = new Words(0, 10922, 10923, 21845, 21846, 32767, 0, 0, 0);
        Assert.Equal(1 + 1 + 2 + 2 + 3 + 3 + 1 + 1 + 1, LevelUp.RollHitDice(16, 4, rng));
        Assert.Equal(0, rng.Remaining); // exactly one word per die
    }
}
