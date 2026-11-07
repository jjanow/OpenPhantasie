using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Combat special spells CHARM … AWAKEN, OV2 jump table <c>0x9d1</c> (combat-resolution.md §Combat spell effects, s31).
/// CHARM, TELEPORTATION, FEAR and DISPELL UNDEAD's target list follow the manual by user decision (D-021…D-024, s32).
/// </summary>
public class CombatSpellsTests
{
    /// <summary>Hands out exactly the scripted 0..99 rolls; any extra draw fails the test (pins DOS draw order).</summary>
    private sealed class StrictRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public StrictRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Remaining => _values.Count;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.NotEmpty(_values);
            return _values.Dequeue();
        }
    }

    /// <summary>Repeats its last value forever (for whole rounds where only the first draws matter).</summary>
    private sealed class LoopRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public LoopRandomSource(params int[] values) => _values = new Queue<int>(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            var v = _values.Count > 1 ? _values.Dequeue() : _values.Peek();
            return Math.Clamp(v, minInclusive, maxExclusive - 1);
        }
    }

    private static Character Caster(int level, int spell)
    {
        var ch = new Character
        {
            Name = "Mage", Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = level,
            HpCur = 50, HpMax = 50, MagicCur = 20, MagicMax = 20, MpCeiling = 20,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            SpellsKnown = new int[SpellId.Count],
        };
        ch.SpellsKnown[spell] = 1;
        return ch;
    }

    private static Monster Mon(
        long xp = 50, int level = 1, int hp = 20, string name = "ORC", bool undead = false, bool neverFlees = false) =>
        new() { Name = name, HitPoints = hp, Level = level, ExperienceValue = xp, Undead = undead, NeverFlees = neverFlees };

    private static CombatEncounter Encounter(Character caster, int monsterLevel, params Monster[][] ranks)
    {
        var party = new Party();
        party.Members.Add(caster);
        var enc = new CombatEncounter(party) { MonsterLevel = monsterLevel };
        foreach (var r in ranks)
            enc.AddRank(r);
        enc.Prepare();
        return enc;
    }

    // --- CHARM (0x767 roll; D-022 manual end) -------------------------------------------

    [Fact]
    public void Charm_TieFails_NoMessage()
    {
        var wiz = Caster(2, SpellId.Charm);
        var enc = Encounter(wiz, 4, new[] { Mon() });
        var log = new List<string>();
        Assert.False(CombatSpells.Charm(enc, wiz, new StrictRandomSource(40, 20), log)); // 80 > 80 is false
        Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
        Assert.Empty(log);
    }

    [OriginalDataFact]
    public void Charm_EndsTheRoundAtOnce_LikeGreetings()
    {
        // D-022: kill XP kept, no gold, treasure forfeited; the monsters never act after the charm.
        var wiz = Caster(2, SpellId.Charm);
        var orc = Mon();
        orc.AttackPower = 300;
        var enc = Encounter(wiz, 4, new[] { orc });
        enc.RewardXp = 12;
        enc.RewardGold = 90;
        enc.SetAction(0, CombatAction.CastSpell(SpellId.Charm));

        var rng = new LoopRandomSource(0, 41, 20); // ready 0, then 41·2 = 82 > 20·4 = 80
        var end = new CombatResolver().ResolveRound(enc, rng);
        Assert.True(end.EncounterOver);
        Assert.False(end.PartyWon);
        Assert.False(end.PartyWiped);
        Assert.Equal(12, end.RewardXp);
        Assert.Equal(0, end.RewardGold);
        Assert.True(enc.TreasureForfeited);
        Assert.Contains("MONSTERS ARE CHARMED", end.Log);
        Assert.DoesNotContain(end.Log, l => l.StartsWith("ORC", StringComparison.Ordinal));
    }

    // --- TELEPORTATION (D-023 manual: always works, at once) ------------------------------

    [Fact]
    public void Teleport_AlwaysWorks_NoRoll()
    {
        var wiz = Caster(1, SpellId.Teleportation);
        var enc = Encounter(wiz, 20, new[] { Mon() });
        CombatSpells.Teleport(enc);
        Assert.Equal(SpellCombatEnd.Teleported, enc.SpellEnd);
    }

    [Fact]
    public void Teleport_EndsBeforeTheMonstersAct_WithKillXpAndNoGold()
    {
        var wiz = Caster(5, SpellId.Teleportation);
        var orc = Mon();
        orc.AttackPower = 300;
        var enc = Encounter(wiz, 1, new[] { orc });
        enc.RewardXp = 33;
        enc.RewardGold = 90;
        enc.SetAction(0, CombatAction.CastSpell(SpellId.Teleportation));

        var rng = new StrictRandomSource(0); // the caster's ready roll only: no teleport roll, no monster draws
        var end = new CombatResolver().ResolveRound(enc, rng);
        Assert.Equal(0, rng.Remaining);
        Assert.DoesNotContain(end.Log, l => l.StartsWith("ORC", StringComparison.Ordinal));
        Assert.True(end.EncounterOver);
        Assert.False(end.PartyWon);
        Assert.Equal(33, end.RewardXp);
        Assert.Equal(0, end.RewardGold);
        Assert.True(enc.TreasureForfeited);
        Assert.DoesNotContain("MONSTERS KILLED", end.Log);
    }

    [OriginalDataFact]
    public void Session_RoundTop_EndsFightBySpell_BeforeMorale()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(data.Monsters.First(m => m.HpBase <= 8).Id);
        session.Combat!.RewardXp = 20;
        session.Combat.RewardGold = 300;
        session.Combat.SpellEnd = SpellCombatEnd.Ended; // e.g. FEAR's run cast last round

        Assert.Equal(MoraleOutcome.EndedBySpell, session.StartCombatRound());
        Assert.Null(session.Combat);
        Assert.Equal(GameMode.Overworld, session.Mode);
        Assert.Equal(20, session.Sack.Experience);
        Assert.Equal(300, session.Sack.Gold);
    }

    // --- FEAR (0x80c roll; D-021 manual run) ---------------------------------------------

    [OriginalDataTheory]
    [InlineData(59, true)]   // 5·(5 − 3) + 50 = 60
    [InlineData(60, false)]
    public void Fear_Works_MonstersRun_WithFullGold(int roll, bool works)
    {
        var wiz = Caster(5, SpellId.Fear);
        var enc = Encounter(wiz, 3, new[] { Mon() });
        enc.RewardXp = 10;
        enc.RewardGold = 40;
        var log = new List<string>();
        Assert.Equal(works, CombatSpells.Fear(enc, wiz, new StrictRandomSource(roll), log));
        Assert.Equal(works ? -3 : 3, enc.MonsterLevel);
        Assert.Equal(works ? SpellCombatEnd.Ended : SpellCombatEnd.None, enc.SpellEnd);
        Assert.Equal(works, enc.MonstersFrozen);
        Assert.Equal(works ? new[] { "MONSTERS RUN" } : Array.Empty<string>(), log);
        if (!works)
            return;

        var end = CombatSpells.EndBySpell(enc);
        Assert.False(end.PartyWon);
        Assert.Equal(10, end.RewardXp);
        Assert.Equal(40, end.RewardGold); // D-019: runners pay full gold
    }

    [Fact]
    public void Fear_NeverFlees_OnlyLowersLevel_Stacks()
    {
        var wiz = Caster(5, SpellId.Fear);
        var enc = Encounter(wiz, 3, new[] { Mon(name: "BLACK LORD", neverFlees: true) });
        var log = new List<string>();
        Assert.True(CombatSpells.Fear(enc, wiz, new StrictRandomSource(59), log));
        Assert.Equal(-3, enc.MonsterLevel);
        Assert.False(CombatSpells.Fear(enc, wiz, new StrictRandomSource(90), log)); // L below 0: 5·(5 + 3) + 50 = 90
        Assert.True(CombatSpells.Fear(enc, wiz, new StrictRandomSource(89), log));
        Assert.Equal(-9, enc.MonsterLevel);
        Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
        Assert.False(enc.MonstersFrozen);
        Assert.Empty(log);
    }

    [OriginalDataFact]
    public void BlackLord_FearThreeTimes_ThenDissolve_Wins()
    {
        // endgame.md §1: L 21 → 15 → 9 → 3, then DISSOLVE needs rand ≤ 110 − 30 = 80.
        var data = GameDataRepository.LoadPhantasie1();
        var lord = data.GetMonster(72).ToCombatant(new StrictRandomSource(50));
        Assert.True(lord.NeverFlees);
        var wiz = Caster(15, SpellId.Fear);
        var enc = Encounter(wiz, 21, new[] { lord });
        var log = new List<string>();
        for (var i = 0; i < 3; i++)
            Assert.True(CombatSpells.Fear(enc, wiz, new StrictRandomSource(0), log));
        Assert.Equal(3, enc.MonsterLevel);
        Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
        Assert.True(CombatSpells.CastDissolve(enc, new StrictRandomSource(80), log));
        Assert.True(CombatSpells.EndBySpell(enc).PartyWon);
    }

    [OriginalDataTheory]
    [InlineData(72, false, true)] // BLACK LORD
    [InlineData(0, true, false)]  // SKELETON
    public void Traits_SurviveTheRealEncounterPath_AndSaveLoad(int id, bool undead, bool neverFlees)
    {
        var data = GameDataRepository.LoadPhantasie1();
        var session = GameSessionFactory.Create(data, new SeededDosRandomSource(11));
        session.OpenGuild();
        var hero = session.CreateAndEnroll("Porter", Race.Human, CharacterClass.Wizard);
        Assert.True(session.TryAddToParty(hero, out _));
        session.DepartGuildToWilderness();
        session.BeginWildernessCombat(id);
        var front = session.Combat!.Ranks[0].Members[0];
        Assert.Equal((undead, neverFlees), (front.Undead, front.NeverFlees));

        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(session.CaptureSnapshot())));
        var again = loaded.Combat!.Ranks[0].Members[0];
        Assert.Equal((undead, neverFlees), (again.Undead, again.NeverFlees));
    }

    // --- DISSOLVE (0x83c / 0x85b / 0x8c1) ------------------------------------------------

    [OriginalDataTheory]
    [InlineData(80, true)]   // 110 − 10·3
    [InlineData(81, false)]
    public void Dissolve_RollsAgainst110MinusTenL(int roll, bool works)
    {
        var wiz = Caster(5, SpellId.Dissolve);
        var enc = Encounter(wiz, 3, new[] { Mon(), Mon() });
        enc.RewardGold = 40;
        var log = new List<string>();
        Assert.Equal(works, CombatSpells.CastDissolve(enc, new StrictRandomSource(roll), log));
        Assert.Equal(works ? 0 : 40, enc.RewardGold);
        Assert.Equal(works, enc.Dissolved);
        Assert.Equal(works ? new[] { "MONSTERS START DISSOLVING" } : Array.Empty<string>(), log);
    }

    [Theory]
    [InlineData(1, 0)]   // last living index 0: a lone front monster pays nothing
    [InlineData(3, 2)]
    [InlineData(6, 5)]
    public void Dissolve_PaysFrontGroupXpTimesLastLivingIndex(int living, int factor)
    {
        var wiz = Caster(5, SpellId.Dissolve);
        var front = Enumerable.Range(0, living).Select(_ => Mon(xp: 100)).ToArray();
        var back = new[] { Mon(xp: 1000), Mon(xp: 1000) }; // other groups pay nothing extra
        var enc = Encounter(wiz, 1, front, back);
        enc.RewardXp = 7;
        CombatSpells.Dissolve(enc, new List<string>());
        Assert.Equal(7 + 100 * factor, enc.RewardXp);
    }

    [Fact]
    public void Dissolve_FrontGroupAlreadyWiped_LosesOneMonstersXp()
    {
        var wiz = Caster(5, SpellId.Dissolve);
        var enc = Encounter(wiz, 1, new[] { Mon(xp: 100, hp: 0) }, new[] { Mon(xp: 1000) });
        enc.RewardXp = 250;
        CombatSpells.Dissolve(enc, new List<string>());
        Assert.Equal(150, enc.RewardXp); // [0x74be] = −1
    }

    [OriginalDataFact]
    public void Dissolve_ThenKillSameRound_PaysBoth()
    {
        // Mage dissolves (2 living → +77), then the fighter kills one of the same group in that round (+77 again).
        var wiz = Caster(5, SpellId.Dissolve);
        var fighter = new Character
        {
            Name = "Hero", Class = CharacterClass.Fighter, Status = CharacterStatus.Ok, Level = 1,
            HpCur = 40, HpMax = 40, WeaponDamageA = 30,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
            Skills = new CharacterSkills(90, 0, 0, 0, 0, 0, 0, 0),
        };
        var party = new Party();
        party.Members.Add(wiz);
        party.Members.Add(fighter);
        var enc = new CombatEncounter(party) { MonsterLevel = 1, RewardGold = 60 };
        enc.AddRank(Mon(xp: 77, hp: 1), Mon(xp: 77, hp: 500));
        enc.Prepare();
        enc.SetAction(0, CombatAction.CastSpell(SpellId.Dissolve));
        enc.SetAction(1, CombatAction.Thrust(0));

        var resolver = new CombatResolver();
        var round = resolver.ResolveRound(enc, new LoopRandomSource(0));
        Assert.False(round.EncounterOver);
        Assert.Contains(round.Log, l => l.EndsWith("DIES", StringComparison.Ordinal));

        var end = resolver.ResolveRound(enc, new StrictRandomSource());
        Assert.True(end.PartyWon);
        Assert.Equal(154, end.RewardXp);
        Assert.Equal(0, end.RewardGold);
        Assert.Contains("MONSTERS KILLED", end.Log);
    }

    // --- DISPELL UNDEAD (0x8fc) ----------------------------------------------------------

    [OriginalDataFact]
    public void Undead_IsTheManualList_NotDemonsOrBantir()
    {
        // D-024: DOS's XP % 7 set minus BANTIR, DEMON, HIGH DEMON (monster-traits.json).
        var data = GameDataRepository.LoadPhantasie1();
        var undead = data.Monsters.Where(m => CombatSpells.IsUndead(m.ToRecord())).Select(m => m.Name).ToArray();
        Assert.Equal(
            new[]
            {
                "SKELETON", "ZOMBIE", "GHOUL", "LARGE SKELETON", "WIGHT", "WRAITH", "VAMPIRE", "GHOST", "SPIRIT",
            }.OrderBy(n => n),
            undead.OrderBy(n => n));
    }

    [Fact]
    public void DispellUndead_NotUndead_NoRoll_CanRetry()
    {
        var priest = Caster(5, SpellId.DispellUndead);
        var enc = Encounter(priest, 1, new[] { Mon(xp: 50) }, new[] { Mon(xp: 77, name: "SKELETON", undead: true) });
        var log = new List<string>();
        CombatSpells.DispellUndead(enc, priest, new StrictRandomSource(), log); // only the front group is checked
        Assert.False(enc.DispellUndeadTried);
        Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
        Assert.Empty(log);
    }

    [OriginalDataTheory]
    [InlineData(0, "MONSTERS START DISSOLVING")]  // v = 5·(5 − 2) − 0 = 15
    [InlineData(19, "MONSTERS START DISSOLVING")] // v = −4
    [InlineData(20, "MONSTERS RUN")]              // v = −5
    [InlineData(59, "MONSTERS RUN")]              // v = −44
    [InlineData(60, null)]                        // v = −45: nothing
    public void DispellUndead_Bands(int roll, string? message)
    {
        var priest = Caster(5, SpellId.DispellUndead);
        var enc = Encounter(priest, 2, new[] { Mon(xp: 154, level: 2, name: "GHOUL", undead: true), Mon(xp: 154, level: 2, undead: true) });
        enc.RewardGold = 80;
        var log = new List<string>();
        CombatSpells.DispellUndead(enc, priest, new StrictRandomSource(roll), log);
        Assert.True(enc.DispellUndeadTried);
        if (message is null)
        {
            Assert.Empty(log);
            Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
            return;
        }

        Assert.Equal(new[] { message }, log);
        Assert.Equal(SpellCombatEnd.Ended, enc.SpellEnd);
        var dissolved = message.StartsWith("MONSTERS START", StringComparison.Ordinal);
        Assert.Equal(dissolved, enc.Dissolved);
        Assert.Equal(!dissolved, enc.MonstersFrozen);
        Assert.Equal(dissolved ? 0 : 80, enc.RewardGold);

        var end = CombatSpells.EndBySpell(enc);
        Assert.Equal(dissolved, end.PartyWon);
        Assert.Equal(dissolved ? 0 : 80, end.RewardGold); // a run pays the full gold
    }

    [Fact]
    public void DispellUndead_WorksOncePerFight()
    {
        var priest = Caster(5, SpellId.DispellUndead);
        var enc = Encounter(priest, 2, new[] { Mon(xp: 77, name: "SKELETON", undead: true) });
        CombatSpells.DispellUndead(enc, priest, new StrictRandomSource(99), new List<string>()); // v too low
        Assert.True(enc.DispellUndeadTried);
        CombatSpells.DispellUndead(enc, priest, new StrictRandomSource(), new List<string>()); // no roll at all
        Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
    }

    [OriginalDataFact]
    public void DispellRun_FreezesReadyMonstersForTheRound()
    {
        var priest = Caster(5, SpellId.DispellUndead);
        var ghoul = Mon(xp: 154, level: 2, name: "GHOUL", undead: true);
        ghoul.AttackPower = 300;
        var enc = Encounter(priest, 2, new[] { ghoul });
        enc.SetAction(0, CombatAction.CastSpell(SpellId.DispellUndead));

        // ready 0, dispel roll 30 → v = −15: run. No monster draws afterwards: frozen monsters don't roll.
        var rng = new StrictRandomSource(0, 30);
        var round = new CombatResolver().ResolveRound(enc, rng);
        Assert.Equal(0, rng.Remaining);
        Assert.Contains("MONSTERS RUN", round.Log);
        Assert.DoesNotContain(round.Log, l => l.StartsWith("GHOUL", StringComparison.Ordinal));
        Assert.False(enc.MonstersFrozen); // cleared at round end
        Assert.Equal(SpellCombatEnd.Ended, enc.SpellEnd);
    }

    [OriginalDataTheory]
    // Round 1 of a normal fight (hold set, released after pass 1, 0x10f3). Every roll 10: the caster is ready in pass 1
    // (10 < 3·5 + 5), FEAR works (10 < 60), DISPELL gives v = 5·(5 − 9) − 10 = −30 → run. DISPELL's runners act again
    // from pass 3 (DOS); FEAR's don't (user, s34).
    [InlineData(SpellId.Fear, false)]
    [InlineData(SpellId.DispellUndead, true)]
    public void RunSpell_InRoundOne_FearHoldsAllRound_DispellReleasedAfterPassOne(int spell, bool monsterActs)
    {
        var caster = Caster(5, spell);
        var ghoul = Mon(xp: 154, level: 9, hp: 500, name: "GHOUL", undead: true);
        ghoul.AttackPower = 300;
        var enc = Encounter(caster, 3, new[] { ghoul });
        enc.Surprise = SurpriseFlags.Normal;
        enc.SetAction(0, CombatAction.CastSpell(spell));

        var round = new CombatResolver().ResolveRound(enc, new LoopRandomSource(10));

        Assert.Contains("MONSTERS RUN", round.Log);
        Assert.Equal(monsterActs, round.Log.Any(l => l.StartsWith("GHOUL", StringComparison.Ordinal)));
        Assert.Equal(SpellCombatEnd.Ended, enc.SpellEnd);
    }

    // --- NINJA (0x7ed / 0x9a1) -----------------------------------------------------------

    [Theory]
    [InlineData(SpellId.NinjaIi, 255, 25, 10, 4)]
    [InlineData(SpellId.NinjaI, 200, 20, 8, 3)]
    public void Ninja_SetsCasterOnly(int spell, int hit, int dmg, int armor, int slashSwings)
    {
        var monk = Caster(3, spell);
        var other = Caster(3, spell);
        var party = new Party();
        party.Members.Add(monk);
        party.Members.Add(other);
        var enc = new CombatEncounter(party);
        enc.AddRank(Mon());
        enc.Prepare();
        var before = (enc.Prep[1].WeaponHitEff, enc.Prep[1].WeaponDamageEff, enc.Prep[1].ArmorEff);
        enc.SetAction(0, CombatAction.CastSpell(spell));

        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(spell), new StrictRandomSource(), new List<string>());

        Assert.Equal((hit, dmg, armor), (enc.Prep[0].WeaponHitEff, enc.Prep[0].WeaponDamageEff, enc.Prep[0].ArmorEff));
        Assert.Equal(before, (enc.Prep[1].WeaponHitEff, enc.Prep[1].WeaponDamageEff, enc.Prep[1].ArmorEff));
        Assert.Equal(slashSwings, CombatAction.SlashSwings(enc.Prep[0].WeaponHitEff)); // D-011: 4 at ≥ 225
    }
}
