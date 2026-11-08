using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T1.6 rest: the damage hook sees every damage event, not only melee and monster specials: party spells on
/// monsters, sprung traps, dungeon script damage and swimming. The Phantasie I set has no damage module, so these paths
/// stay exactly the base step.
/// </summary>
public class DamageHookCoverageTests
{
    /// <summary>Records every hit it sees and optionally adds a fixed amount.</summary>
    private sealed class RecordingModule(int add = 0, bool quiet = false) : IDamageModule
    {
        public FeatureModuleDescriptor Descriptor { get; } =
            new("test.recording", GameId.P1, Implemented: true, "test", "test");
        public List<(DamageSource Source, int Amount, int? Spell, int? Trap, bool InCombat)> Before { get; } = [];
        public int After { get; private set; }

        public void BeforeDamage(DamageHit hit, List<string> log)
        {
            Assert.True(hit.Member is null ^ hit.Monster is null);
            Assert.NotNull(hit.Party);
            Before.Add((hit.Source, hit.Amount, hit.SpellIndex, hit.TrapLevel, hit.Encounter is not null));
            hit.Amount += add;
            if (!quiet)
                log.Add("HOOK " + hit.Source);
        }

        public void AfterDamage(DamageHit hit, bool killed, List<string> log) => After++;
    }

    private static FeatureModuleSet With(IDamageModule m) =>
        FeatureModuleSet.Create(GameId.P1, new P1CombatOrdersModule(), m);

    private static Character Wizard(int spell) => new()
    {
        Name = "WIZARD", Class = CharacterClass.Wizard, Level = 5, Status = CharacterStatus.Ok,
        Stats = new CharacterStats(14, 16, 12, 13, 11, 9),
        HpCur = 30, HpMax = 30, MagicMax = 60, MagicCur = 60,
        SpellsKnown = { [spell] = 1 },
    };

    private static Monster Orc() => new()
    {
        Name = "ORC", HitPoints = 200, AttackPower = 50, DamageField = 5, Armor = 1, Defense = 10, CombatRating = 10,
        ExperienceValue = 7,
    };

    private static (CombatEncounter Enc, Monster[] Mons) Fight(int spell, FeatureModuleSet? modules)
    {
        var party = new Party();
        party.Members.Add(Wizard(spell));
        var enc = new CombatEncounter(party);
        var mons = new[] { Orc(), Orc() };
        enc.AddRank(mons);
        enc.Prepare();
        if (modules is not null)
            enc.Modules = modules;
        return (enc, mons);
    }

    [Theory]
    [InlineData(SpellId.FireflashI, 1)]
    [InlineData(SpellId.MindblastI, 2)]
    [InlineData(SpellId.FlameboltI, 2)]
    public void PartySpells_RunTheHook(int spell, int hits)
    {
        var (plainEnc, plainMons) = Fight(spell, null);
        var plainLog = new List<string>();
        SpellCaster.ResolveCombatCast(plainEnc, 0, CombatAction.CastSpell(spell), new SeededRandomSource(9), plainLog);
        var plainLoss = plainMons.Sum(m => 200 - m.HitPoints!.Value);
        Assert.True(plainLoss > 0);

        var hook = new RecordingModule(add: 3);
        var (enc, mons) = Fight(spell, With(hook));
        var log = new List<string>();
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(spell), new SeededRandomSource(9), log);

        Assert.Equal(hits, hook.Before.Count);
        Assert.Equal(hits, hook.After);
        Assert.All(hook.Before, b =>
        {
            Assert.Equal(DamageSource.PartySpell, b.Source);
            Assert.Equal(spell, b.Spell);
            Assert.True(b.InCombat);
        });
        Assert.Equal(plainLoss + 3 * hits, mons.Sum(m => 200 - m.HitPoints!.Value));
        Assert.Contains("HOOK PartySpell", log);
    }

    [Fact]
    public void PhantasieISet_SpellCast_IsUnchanged()
    {
        var (a, am) = Fight(SpellId.FlameboltI, null);
        var (b, bm) = Fight(SpellId.FlameboltI, FeatureModuleSet.ForGame(GameId.P1));
        var la = new List<string>();
        var lb = new List<string>();
        SpellCaster.ResolveCombatCast(a, 0, CombatAction.CastSpell(SpellId.FlameboltI), new SeededRandomSource(4), la);
        SpellCaster.ResolveCombatCast(b, 0, CombatAction.CastSpell(SpellId.FlameboltI), new SeededRandomSource(4), lb);
        Assert.Equal(la, lb);
        Assert.Equal(am.Select(m => m.HitPoints), bm.Select(m => m.HitPoints));
    }

    [Fact]
    public void PassThroughHook_KeepsTheGoldenTrace()
    {
        // A module that sees every hit but changes nothing leaves the fight (RNG calls, log, HP) exactly as I's.
        foreach (var seed in new[] { 1, 77, 2026 })
        {
            var hook = new RecordingModule(quiet: true);
            Assert.Equal(CombatTraceGoldenTests.RunFight(seed), CombatTraceGoldenTests.RunFight(seed, e => e.Modules = With(hook)));
            Assert.Contains(hook.Before, b => b.Source == DamageSource.PartySpell);
        }
    }

    private static Character Walker(string name, int hp = 30, int dex = 0, int luck = 1) => new()
    {
        Name = name, Class = CharacterClass.Fighter, Status = CharacterStatus.Ok, HpCur = hp, HpMax = hp,
        Stats = new CharacterStats(10, 10, dex, 10, 10, luck),
    };

    private static Party PartyOf(FeatureModuleSet? modules, params Character[] members)
    {
        var p = new Party();
        if (modules is not null)
            p.Modules = modules;
        p.Members.AddRange(members);
        return p;
    }

    [Fact]
    public void Trap_RunsTheHook_OutsideCombat()
    {
        var plain = PartyOf(null, Walker("A"), Walker("B"));
        var plainLog = new List<string>();
        DungeonTraps.Spring(plain, 4, new SeededRandomSource(5), plainLog, out var plainTotal);

        var hook = new RecordingModule(add: 2);
        var party = PartyOf(With(hook), Walker("A"), Walker("B"));
        var log = new List<string>();
        DungeonTraps.Spring(party, 4, new SeededRandomSource(5), log, out var total);

        Assert.Equal(2, hook.Before.Count); // DEX 0: nobody avoids it
        Assert.Equal(2, hook.After);
        Assert.All(hook.Before, b =>
        {
            Assert.Equal(DamageSource.Trap, b.Source);
            Assert.Equal(4, b.Trap);
            Assert.False(b.InCombat);
        });
        Assert.Equal(plainTotal + 4, total);
        Assert.Equal(plain.Members.Select(m => m.HpCur - 2), party.Members.Select(m => m.HpCur));
        Assert.Contains("HOOK Trap", log);
        Assert.Equal(plainLog, log.Where(l => !l.StartsWith("HOOK ")));
    }

    [Fact]
    public void Swimming_RunsTheHook()
    {
        var hook = new RecordingModule(add: 1);
        var party = PartyOf(With(hook), Walker("A"));
        var plain = PartyOf(null, Walker("A"));
        Swimming.Resolve(plain, new SeededRandomSource(3), new List<string>());
        Swimming.Resolve(party, new SeededRandomSource(3), new List<string>());

        Assert.Equal(DamageSource.Swimming, Assert.Single(hook.Before).Source);
        Assert.Equal(plain.Members[0].HpCur - 1, party.Members[0].HpCur);
    }

    [OriginalDataFact]
    public void ScriptDamageOps_RunTheHook()
    {
        // A one-op script at the script base: op 1 (`core.script.smoke_fills_the_air`, damage a = 5), no continue flag.
        static (DungeonLevel, DungeonScriptResume) Script()
        {
            var level = GameDataRepository.LoadPhantasie1().Dungeons.Single(d => d.Id == "DNG1").ToRuntime().CloneAsBuffer();
            level.SetBufferByte(DungeonScript.ScriptBase, 1);
            level.SetBufferByte(DungeonScript.ScriptBase + 1, 5);
            return (level, new DungeonScriptResume { NextOp = DungeonScript.ScriptBase });
        }

        var (plainLevel, plainAt) = Script();
        var plain = PartyOf(null, Walker("A", luck: 10), Walker("B", luck: 10));
        DungeonScript.Resume(new DungeonState(), plainLevel, plain, new NoHost(), new SeededRandomSource(8), plainAt);

        var hook = new RecordingModule(add: 1);
        var (level, at) = Script();
        var party = PartyOf(With(hook), Walker("A", luck: 10), Walker("B", luck: 10));
        var r = DungeonScript.Resume(new DungeonState(), level, party, new NoHost(), new SeededRandomSource(8), at);

        Assert.Equal(2, hook.Before.Count);
        Assert.All(hook.Before, b => Assert.Equal(DamageSource.DungeonScript, b.Source));
        Assert.Contains("HOOK DungeonScript", r.Log);
        Assert.Equal(plain.Members.Select(m => m.HpCur - 1), party.Members.Select(m => m.HpCur));
    }

    private sealed class NoHost : IDungeonScriptHost
    {
        public long FoundGold { get; set; }
        public int ShowMessage(int messageId) => 1;
    }

    [OriginalDataFact]
    public void Session_HandsItsModules_ToTheParty()
    {
        var session = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(7));
        Assert.Same(session.FeatureModules, session.Party.Modules);
        var custom = With(new RecordingModule());
        session.FeatureModules = custom;
        Assert.Same(custom, session.Party.Modules);
    }
}
