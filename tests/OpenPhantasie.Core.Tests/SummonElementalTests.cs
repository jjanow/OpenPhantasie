using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// `core.spell.summon_elemental` end to end (combat-resolution.md §`core.spell.summon_elemental`, s33): Guild prep into party slot 7 (OV1
/// <c>0x2786</c>), the cast (OV2 <c>0x8cc</c>), combat membership, end of combat (<c>0x3f2e</c>) and the ways it leaves.
/// </summary>
public class SummonElementalTests
{
    private static GameDataRepository Data => GameDataRepository.LoadPhantasie1();

    private sealed class FixedRandomSource(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    private static Character Member(string name, int spell = -1)
    {
        var ch = new Character
        {
            Name = name, Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = 5,
            HpCur = 30, HpMax = 30, MagicCur = 20, MagicMax = 20, MpCeiling = 20,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            SpellsKnown = new int[SpellId.Count],
        };
        if (spell >= 0)
            ch.SpellsKnown[spell] = 1;
        return ch;
    }

    private static Character Fire() => Data.Elementals[0].ToCharacter();

    /// <summary>Six members (member 0 knows SUMMON), FIRE ELEM loaded, one weak monster.</summary>
    private static CombatEncounter SixWithElemental(ElementalPrepRule rule = ElementalPrepRule.Dos)
    {
        var party = new Party();
        party.Members.Add(Member("CASTER", SpellId.SummonElemental));
        for (var i = 1; i < Party.MaxMembers; i++)
            party.Members.Add(Member($"M{i}"));
        Assert.True(GuildHall.TryAddElemental(party, Fire(), out _));
        var enc = new CombatEncounter(party) { MonsterLevel = 1, ElementalPrep = rule };
        enc.AddRank(new Monster { Name = "ORC", HitPoints = 20, Level = 1, ExperienceValue = 10, Defense = 0 });
        enc.Prepare();
        return enc;
    }

    // --- Data ---------------------------------------------------------------------------

    [OriginalDataFact]
    public void Templates_AreTheTwnsIntRecords()
    {
        var e = Data.Elementals;
        Assert.Equal(new[] { "FIRE ELEM", "WATER ELEM", "EARTH ELEM" }, e.Select(x => x.Name));
        Assert.Equal(new[] { 100, 150, 200 }, e.Select(x => x.HpMax));
        Assert.Equal(new[] { 200, 150, 100 }, e.Select(x => x.Attack));
        Assert.Equal(new[] { 60, 80, 100 }, e.Select(x => x.Parry));
        Assert.Equal(new[] { 40, 30, 20 }, e.Select(x => x.WeaponDamageB));
        Assert.Equal(new[] { 7, 14, 21 }, e.Select(x => x.ArmorSoak));
        Assert.Equal(new[] { 23, 24, 25 }, e.Select(x => x.GuildNumber));

        var fire = Fire();
        Assert.Equal(CharacterClass.Fighter, fire.Class);
        Assert.Equal(Race.Undead, fire.Race);
        Assert.Equal(20, fire.Level);
        Assert.Equal(100, fire.ShieldSoak);
        Assert.Equal(20, fire.MagicCur);
        Assert.Equal(new[] { 4, 5, 6, 7 }, Enumerable.Range(0, SpellId.Count).Where(k => fire.SpellsKnown[k] == 1));
        Assert.True(Magic.KnowsSpell(fire, SpellId.FireflashI));
        Assert.False(Magic.KnowsSpell(fire, SpellId.HealingI)); // slot value 2 = available, not known
    }

    // --- Guild prep (OV1 0x2786) -----------------------------------------------------------

    [OriginalDataFact]
    public void Guild_ElementalNeedsSixMembers_OneAtATime_ThenAddIsRefused()
    {
        var party = new Party();
        for (var i = 0; i < 5; i++)
            party.Members.Add(Member($"M{i}"));
        Assert.False(GuildHall.TryAddElemental(party, Fire(), out _)); // list shows 22 rows only

        party.Members.Add(Member("M5"));
        var template = Fire();
        Assert.True(GuildHall.TryAddElemental(party, template, out _));
        Assert.NotSame(template, party.Elemental);            // fresh copy, the template is never written back
        Assert.Equal(6, party.Members.Count);                 // party size stays 6, slot 7 is separate
        Assert.False(party.Elemental!.InParty);
        Assert.False(GuildHall.TryAddElemental(party, Fire(), out _));

        var roster = new GuildRoster();
        var spare = Member("SPARE");
        roster.Slots[10] = spare;
        Assert.False(GuildHall.TryAddToParty(roster, party, spare, 1, out _)); // 0x1fe8: Add refused while loaded
    }

    [OriginalDataFact]
    public void Guild_DropOrPurgeOfAPartyMember_ClearsTheElemental()
    {
        var enc = SixWithElemental();
        var party = enc.Party;
        Assert.True(GuildHall.TryDropFromParty(party, party.Members[5], out _));
        Assert.Null(party.Elemental);

        party.Members.Add(Member("BACK"));
        Assert.True(GuildHall.TryAddElemental(party, Fire(), out _));
        var roster = new GuildRoster();
        roster.Slots[0] = party.Members[0];
        Assert.True(GuildHall.TryPurge(roster, party, party.Members[0], out _));
        Assert.Null(party.Elemental);
    }

    [OriginalDataFact]
    public void TownEntry_ElementalLeaves()
    {
        var enc = SixWithElemental();
        Aging.ApplyTownEntry(enc.Party, 1);
        Assert.Null(enc.Party.Elemental);
        Assert.Equal(6, enc.Party.Members.Count);
    }

    // --- The cast (OV2 0x8cc) -----------------------------------------------------------------

    [OriginalDataFact]
    public void Summon_SixLiving_JoinsAsMemberSeven_WithNoActionThisRound()
    {
        var enc = SixWithElemental();
        var elemental = enc.Party.Elemental!;
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(7, enc.Party.Members.Count);
        Assert.Same(elemental, enc.Party.Members[6]);
        Assert.True(enc.Party.ElementalSummoned);
        Assert.Equal(7, enc.Prep.Length);
        Assert.Equal(CombatAction.Cleared, enc.Prep[6].PackedAction); // action 4 from the build (0x339e)
        Assert.False(CombatSpells.SummonElemental(enc));             // already fighting
    }

    [OriginalDataFact]
    public void Summon_ThreatenAverageLevel_IgnoresTheElemental()
    {
        // [0x63ec] is set at town Exit over party size ≤ 6; the summoned level-20 elemental never enters it.
        var enc = SixWithElemental();
        var before = PartyOptions.AveragePartyLevel(enc.Party);
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(before, PartyOptions.AveragePartyLevel(enc.Party));
        Assert.Equal(5, before);
    }

    [OriginalDataFact]
    public void Summon_KeepsTheOthersPrep()
    {
        var enc = SixWithElemental();
        enc.SetAction(1, CombatAction.Thrust(0));
        enc.Prep[2].WeaponHitEff = 255; // e.g. NINJA II
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(CombatAction.Thrust(0), enc.Prep[1].PackedAction);
        Assert.Equal(255, enc.Prep[2].WeaponHitEff);
    }

    [OriginalDataFact]
    public void Summon_SleepCountsAsLiving_DeadMemberBlocksIt()
    {
        var enc = SixWithElemental();
        enc.Party.Members[3].Status = CharacterStatus.Sleep;
        Assert.True(CombatSpells.SummonElemental(enc));

        // D-033 (manual): six non-DEAD members — a DEAD member blocks it (the check has no pass input, so every pass).
        var enc2 = SixWithElemental();
        enc2.Party.Members[3].Status = CharacterStatus.Dead;
        Assert.False(CombatSpells.SummonElemental(enc2));
        Assert.Equal(6, enc2.Party.Members.Count);
    }

    [OriginalDataFact]
    public void Summon_WithoutGuildPrep_Fails()
    {
        var enc = SixWithElemental();
        enc.Party.Elemental = null;
        Assert.False(CombatSpells.SummonElemental(enc));
    }

    [OriginalDataFact]
    public void Cast_SpendsMpEitherWay_AndPrintsNothing()
    {
        var enc = SixWithElemental();
        enc.Party.Members[3].Status = CharacterStatus.Dead; // will fail
        var caster = enc.Party.Members[0];
        var log = new List<string>();
        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.SummonElemental),
            new FixedRandomSource(0), log);
        Assert.True(r.Success);
        Assert.Equal(16, caster.MagicCur); // 4 MP: (47 % 4) + 1
        Assert.Single(log);                // only the generic "casts spell" line — 0x8cc prints nothing
        Assert.Equal(6, enc.Party.Members.Count);
    }

    [OriginalDataFact]
    public void Resolver_SummonMidRound_ElementalWaitsUntilNextRound()
    {
        var enc = SixWithElemental();
        enc.AllMonsters.First().AttackPower = 0;
        enc.SetAction(0, CombatAction.CastSpell(SpellId.SummonElemental));
        var round = new CombatResolver().ResolveRound(enc, new FixedRandomSource(0));
        Assert.False(round.EncounterOver);
        Assert.Equal(7, enc.Party.Members.Count);
        Assert.DoesNotContain(round.Log, l => l.StartsWith("FIRE ELEM", StringComparison.Ordinal));
    }

    // --- Combat fields: D-032 normal prep (default) vs DOS (never prepped) ----------------------------------

    [OriginalDataFact]
    public void ElementalPrep_Dos_IsTheStoredZeros_PreparedIsTheNormalPrep()
    {
        var dos = CombatSpells.ElementalPrep(Fire(), ElementalPrepRule.Dos);
        Assert.Equal((0, 0, 0, 0, 100), (dos.WeaponHitEff, dos.WeaponDamageEff, dos.ArmorEff, dos.DefenseRating, dos.ShieldSoak));

        var prepared = CombatSpells.ElementalPrep(Fire(), ElementalPrepRule.Prepared);
        Assert.Equal((200, 40, 7, 100), (prepared.WeaponHitEff, prepared.WeaponDamageEff, prepared.ArmorEff, prepared.ShieldSoak));
        Assert.Equal(CombatMath.ComputeDefenseRating(0, 7, 60, 100), prepared.DefenseRating);

        var enc = SixWithElemental(ElementalPrepRule.Prepared);
        Assert.True(CombatSpells.SummonElemental(enc));
        Assert.Equal(200, enc.Prep[6].WeaponHitEff);
    }

    // --- Session: every combat exit, survivors, next encounter, save/load ------------------------

    private static GameSession SessionWithSix(out GameDataRepository data)
    {
        data = Data;
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(5));
        session.OpenGuild();
        for (var i = 0; i < Party.MaxMembers; i++)
        {
            var hero = session.CreateAndEnroll($"HERO{i}", Race.Human, CharacterClass.Fighter);
            Assert.True(session.TryAddToParty(hero, out _));
        }
        Assert.Equal(3, session.ElementalTemplates.Count);
        Assert.True(session.TryAddElemental(2, out _)); // EARTH ELEM
        session.DepartGuildToWilderness();
        return session;
    }

    private static void SummonInto(GameSession session, GameDataRepository data)
    {
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        Assert.True(CombatSpells.SummonElemental(session.Combat!));
        Assert.Equal(7, session.Party.Members.Count);
    }

    [OriginalDataFact]
    public void Victory_ElementalGoesBackToSlot7_AndIsSummonableNextFight()
    {
        var session = SessionWithSix(out var data);
        SummonInto(session, data);
        var earth = session.Party.Elemental!;
        earth.HpCur = 50;
        earth.Status = CharacterStatus.Dead;
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        session.Combat.RewardXp = 60;

        var end = session.ResolveCombatRound();
        Assert.True(end.PartyWon);
        Assert.Equal(6, session.Party.Members.Count);
        Assert.Same(earth, session.Party.Elemental);   // still loaded for the trip
        Assert.Equal(CharacterStatus.Dead, earth.Status); // not woken at the end (0x3eff runs over 6)
        Assert.Equal(60, session.Sack.Experience);

        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        Assert.Equal(CharacterStatus.Ok, earth.Status); // build 0x352f
        Assert.Equal(50, earth.HpCur);                   // HP untouched
        Assert.True(CombatSpells.SummonElemental(session.Combat!));
    }

    [OriginalDataFact]
    public void AllSixDead_ElementalStanding_CountsAsDefeat()
    {
        var session = SessionWithSix(out var data);
        SummonInto(session, data);
        foreach (var hero in session.Party.Adventurers)
        {
            hero.HpCur = 0;
            hero.Status = CharacterStatus.Dead;
        }
        foreach (var m in session.Combat!.AllMonsters)
            m.HitPoints = 0;
        session.Combat.RewardXp = 60;

        var end = session.ResolveCombatRound();
        Assert.True(end.PartyWiped);   // state 6 (0x3fb2): the survivor count skips slot 7
        Assert.Equal(0, session.Sack.Experience);
        Assert.Null(session.Party.Elemental); // defeat removes it (OV1 0x3bc)
        Assert.Equal(6, session.Party.Members.Count);
    }

    [OriginalDataFact]
    public void PartyOptionExit_DismissesTheElemental()
    {
        var session = SessionWithSix(out var data);
        SummonInto(session, data);
        var r = session.ChoosePartyOption(PartyOption.BegMercy);
        Assert.Equal(PartyOptionOutcome.CombatOver, r.Outcome);
        Assert.Equal(6, session.Party.Members.Count);
        Assert.NotNull(session.Party.Elemental);
    }

    [OriginalDataFact]
    public void SpellEndExit_DismissesTheElemental()
    {
        var session = SessionWithSix(out var data);
        SummonInto(session, data);
        session.Combat!.SpellEnd = SpellCombatEnd.Ended;
        Assert.Equal(MoraleOutcome.EndedBySpell, session.StartCombatRound());
        Assert.Equal(6, session.Party.Members.Count);
        Assert.NotNull(session.Party.Elemental);
    }

    [OriginalDataFact]
    public void EnterTown_DropsTheElemental()
    {
        var session = SessionWithSix(out _);
        session.EnterTown(1);
        Assert.Null(session.Party.Elemental);
    }

    [OriginalDataFact]
    public void SaveLoad_KeepsSlot7_InAndOutOfCombat()
    {
        var session = SessionWithSix(out var data);
        var calm = SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot()));
        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(calm);
        Assert.Equal("EARTH ELEM", loaded.Party.Elemental!.Name);
        Assert.Equal(6, loaded.Party.Members.Count);

        SummonInto(session, data);
        session.Party.Elemental!.HpCur = 77;
        var fighting = SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot()));
        Assert.Equal(6, fighting.PartySlotIndices.Length);
        var again = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        again.ApplySnapshot(fighting);
        Assert.Equal(7, again.Party.Members.Count);
        Assert.True(again.Party.ElementalSummoned);
        Assert.Equal(77, again.Party.Elemental!.HpCur);
        Assert.Equal(7, again.Combat!.Prep.Length);
        Assert.Equal(100, again.Combat.Prep[6].WeaponHitEff); // D-032: normal prep (default), EARTH attack 100
    }
}
