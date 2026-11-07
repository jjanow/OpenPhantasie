using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie II's repeats of I's manual-vs-DOS decisions (user s80, <c>DIFFERENCES-II.md</c> D2-019…D2-023): each plays the
/// manual by default and the Atari ST's behaviour as a <c>p2.*</c> rules option (Original (Atari ST) preset). The session
/// checks run on a stand-in II campaign (the real one is built elsewhere) with II's rules profile and creation tables.
/// </summary>
public class Phantasie2DecidedRulesTests
{
    private static readonly GameTables P2 = GameTables.Load(GameId.P2);
    private static readonly GameDataRepository P1Data = GameDataRepository.LoadPhantasie1();

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P2);
    private static readonly GameOptions St = GameOptions.ForPreset(GameId.P2, OptionPreset.Dos);

    private sealed class StrictRandomSource(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.NotEmpty(_values);
            return _values.Dequeue();
        }
    }

    public static TheoryData<string, string> Options => new()
    {
        { "p2.gold-cap", "D2-019" }, { "p2.guild-size", "D2-020" }, { "p2.lifespans", "D2-021" },
        { "p2.dispell-undead", "D2-022" }, { "p2.fear", "D2-023" },
    };

    [OriginalDataTheory]
    [MemberData(nameof(Options))]
    public void Option_IsManualOrSt_OnlyTheStPresetPlaysSt(string id, string difference)
    {
        var d = RuleOptionRegistry.For(GameId.P2).Single(o => o.Id == id);
        Assert.Equal((GameId.P2, difference), (d.Game, d.Difference));
        Assert.Equal(["manual", "st"], d.Values);
        Assert.Equal(("manual", "st", "manual"), (d.Manual, d.Dos, d.Remaster));
        Assert.Equal("manual", Manual.Get(id));
        Assert.Equal("st", St.Get(id));
        Assert.Equal("manual", GameOptions.ForPreset(GameId.P2, OptionPreset.Remaster).Get(id));
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P1), o => o.Id == id); // II's own; I keeps its p1.* option
    }

    /// <summary>
    /// D2-045 (user s92, audit finished s95): I's decisions hold for II on every shared rule the ST plays as DOS I, with no
    /// ST option. A II save carries no <c>p1.*</c> value, so every II preset, the "Original (Atari ST)" one included,
    /// takes I's decided side.
    /// </summary>
    public static TheoryData<string> AuditedSharedRules => new()
    {
        "p1.town-healing", "p1.attack-gate", "p1.lunge", "p1.binding", "p1.confusion-iv", "p1.beg-mercy", "p1.sack-size",
        "p1.sleep", "p1.charm", "p1.teleport", "p1.spell-level", "p1.fireflash-damage", "p1.fireflash-target",
        "p1.wild-treasure", "p1.wild-inn-dead", "p1.monster-eval", "p1.vision", "p1.cast-flag", "p1.summon-party",
        "p1.failed-run", "p1.elemental-prep", "p1.cast-list", "p1.combat-spell-list", "p1.rename-party", "p1.add-aging",
        "p1.continue", "p1.orders-reset", "p1.stunning-blow", "p1.armor-reduced", "p1.payout-remainder",
        "p1.armory-restock",
    };

    [Theory]
    [MemberData(nameof(AuditedSharedRules))]
    public void D2045_EveryIiPreset_PlaysI_sDecidedSide(string id)
    {
        var d = RuleOptionRegistry.For(GameId.P1).Single(o => o.Id == id);
        Assert.NotEqual(d.Manual, d.Dos);
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P2), o => o.Id == id);
        foreach (var preset in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster })
        {
            var ii = GameOptions.ForPreset(GameId.P2, preset);
            Assert.False(ii.Is(d, d.Dos), $"{id} under II's {preset} preset");
            Assert.All(d.Values.Where(v => v != d.Manual), v => Assert.False(ii.Is(d, v)));
        }
    }

    [OriginalDataFact]
    public void D2045_TypedAccessors_StayOnI_sSide_UnderTheStPreset()
    {
        Assert.False(St.DosArmoryRestock);
        Assert.False(St.IsDos(RuleOptionRegistry.FailedRun));
    }

    [OriginalDataFact]
    public void CombinedAccessors_AnswerBothGames()
    {
        Assert.Equal(GoldCapRule.Manual, Manual.GoldCap);
        Assert.False(Manual.OriginalGuildSize || Manual.OriginalLifespans || Manual.OriginalDispellUndead || Manual.OriginalFear);
        Assert.Equal(GoldCapRule.Dos, St.GoldCap);
        Assert.True(St.OriginalGuildSize && St.OriginalLifespans && St.OriginalDispellUndead && St.OriginalFear);
        // I's own options drive the same accessors.
        var dos = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos);
        Assert.True(dos.OriginalGuildSize && dos.OriginalLifespans && dos.OriginalDispellUndead && dos.OriginalFear);
        Assert.Equal(GoldCapRule.Lifted, GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster).GoldCap);
        // One II option on its own is Custom and changes only its accessor.
        var one = Manual.With("p2.fear", "st");
        Assert.Equal(OptionPreset.Custom, one.Preset);
        Assert.True(one.OriginalFear);
        Assert.False(one.OriginalDispellUndead);
    }

    [OriginalDataFact]
    public void FearBosses_StandByDefault_FleeOption_LetsThemRun()
    {
        var d = RuleOptionRegistry.For(GameId.P2).Single(o => o.Id == "p2.fear-bosses");
        Assert.Equal(("stand", "stand", "stand"), (d.Manual, d.Dos, d.Remaster));
        Assert.False(Manual.FearBossesFlee || St.FearBossesFlee);
        Assert.False(GameOptions.ManualPhantasieI.FearBossesFlee); // I has no such option: the Black Lord stands

        var pluto = P2.Monsters.Single(m => m.Id == 79).ToRecord();
        var wiz = new Character { Name = "WIZ", Level = 20 };
        foreach (var (rules, runs) in new[] { (Manual, false), (Manual.With("p2.fear-bosses", "flee"), true) })
        {
            var party = new Party();
            party.Members.Add(wiz);
            var enc = new CombatEncounter(party) { MonsterLevel = 5, Rules = rules };
            enc.AddRank([new Monster { Name = pluto.Name, HitPoints = 10, NeverFlees = pluto.NeverFlees }]);
            enc.Prepare();
            Assert.True(CombatSpells.Fear(enc, wiz, new StrictRandomSource(0), []));
            Assert.Equal(runs, enc.MonstersFrozen);
        }
    }

    [OriginalDataFact]
    public void P2Options_RoundTripInTheSave()
    {
        var restored = GameOptions.Restore(GameId.P2, "custom",
            new Dictionary<string, string> { ["p2.gold-cap"] = "st", ["p2.lifespans"] = "st" }, null);
        Assert.Equal(GoldCapRule.Dos, restored.GoldCap);
        Assert.True(restored.OriginalLifespans);
        Assert.False(restored.OriginalGuildSize);
        Assert.Equal(St.Values, GameOptions.Restore(GameId.P2, "dos", St.Values, null).Values);
    }

    // ==== D2-022: DISPELL UNDEAD ====================================================================================

    private static readonly string[] StXpDivisibleBy7 =
    [
        "SKELETON", "BANTIR", "ZOMBIE", "GHOUL", "LARGE SKELETON", "WIGHT", "WRAITH", "DEMON", "VAMPIRE", "GHOST",
        "HIGH DEMON", "SPIRIT",
    ];

    [OriginalDataFact]
    public void D2022_ManualUndead_IsIsListPlusPlutosUndeadSnake()
    {
        var undead = P2.Monsters.Where(m => m.ToRecord().Undead).Select(m => m.Name).ToArray();
        Assert.Equal(["SKELETON", "ZOMBIE", "GHOUL", "LARGE SKELETON", "WIGHT", "WRAITH", "VAMPIRE", "GHOST", "SPIRIT",
            "PLUTO'S UNDEAD SNAKE"], undead);
        Assert.Equal(72, P2.Monsters.Single(m => m.Name == "PLUTO'S UNDEAD SNAKE").Id);
        // User s81: Pluto, his Ice Dragon and his eight minions stand fast against FEAR (endgame.md §3.1).
        Assert.Equal([17, 43, 72, 73, 74, 75, 76, 77, 78, 79],
            P2.Monsters.Where(m => m.ToRecord().NeverFlees).Select(m => m.Id));
    }

    [OriginalDataFact]
    public void D2022_StTest_IsXpDivisibleBy7_TheStsTwelve()
    {
        var hit = P2.Monsters.Select(m => m.ToRecord()).Where(m => CombatSpells.IsUndead(m, dos: true)).Select(m => m.Name);
        Assert.Equal(StXpDivisibleBy7, hit);
        var snake = P2.Monsters[72].ToRecord();
        Assert.True(CombatSpells.IsUndead(snake));
        Assert.False(CombatSpells.IsUndead(snake, dos: true)); // XP 25,000: the ST misses it
    }

    [OriginalDataTheory]
    [InlineData(false, 72, true)]  // manual: the snake is undead
    [InlineData(true, 72, false)]  // ST: XP 25,000 is not divisible by 7
    [InlineData(false, 1, false)]  // manual: BANTIR is not undead
    [InlineData(true, 1, true)]    // ST: BANTIR's XP 147 = 7 · 21
    public void D2022_DispellUndead_FollowsTheSavesOption(bool st, int monster, bool works)
    {
        var mon = P2.Monsters[monster].ToCombatant(new SeededDosRandomSource(3));
        var enc = Fight(st ? St : Manual, Caster(SpellId.DispellUndead, level: 15), [mon]);
        var log = new List<string>();
        CombatSpells.DispellUndead(enc, enc.Party.Members[0], new Scripted(0), log);
        Assert.Equal(works, enc.DispellUndeadTried);
        Assert.Equal(works, enc.SpellEnd == SpellCombatEnd.Ended);
    }

    // ==== D2-023: FEAR ==============================================================================================

    [OriginalDataTheory]
    [InlineData(false, SpellCombatEnd.Ended)] // manual: the monsters run
    [InlineData(true, SpellCombatEnd.None)]   // ST: L −= 6 only, the fight goes on
    public void D2023_Fear_FollowsTheSavesOption(bool st, SpellCombatEnd end)
    {
        var enc = Fight(st ? St : Manual, Caster(SpellId.Fear, level: 5), [P2.Monsters[8].ToCombatant(new SeededDosRandomSource(3))]);
        enc.MonsterLevel = 5;
        Assert.True(CombatSpells.Fear(enc, enc.Party.Members[0], new Scripted(0), []));
        Assert.Equal(-1, enc.MonsterLevel);
        Assert.Equal(end, enc.SpellEnd);
        Assert.Equal(!st, enc.MonstersFrozen);
    }

    // ==== D2-019 / D2-020 / D2-021 in a II session ==================================================================

    [OriginalDataFact]
    public void D2019_GoldCap_65000Manual_65535St()
    {
        Assert.Equal((65_000, 65_000), (P2.Rules.PartyGoldCap, P2.Rules.BankGoldCap));
        var session = NewSession();
        session.StartNewGame(Manual);
        Assert.Equal((65_000, 65_000), (session.Party.GoldCap, session.Party.BankGoldCap));
        session.StartNewGame(St);
        Assert.Equal((GoldCaps.Dos, GoldCaps.Dos), (session.Party.GoldCap, session.Party.BankGoldCap));
        Assert.Equal(GoldCaps.Dos, session.Sack.GoldCap);
    }

    [OriginalDataFact]
    public void D2020_GuildSize_21Manual_22St_FromTheStsOwnTable()
    {
        Assert.Equal("atari-st", P2.Rules.OriginalPlatform);
        Assert.Equal("dos", P1Data.Rules.OriginalPlatform);
        var session = NewSession();
        session.StartNewGame(Manual);
        Assert.Equal(21, session.Roster.MaxCharacters);
        session.StartNewGame(St);
        Assert.Equal(22, session.Roster.MaxCharacters); // guild_by_platform.atari-st, not I's "dos" row
        session.StartNewGame(St.With("guild-limit", "none"));
        Assert.True(session.Roster.Unlimited); // the every-game QoL still overrides it
    }

    [OriginalDataFact]
    public void D2021_Lifespans_ManualBands_OrStFactors()
    {
        // The ST's factors are DOS I's (create_character 0x0cc84, jump table 0x22976).
        var st = P2.CreationTables.ForRules(false, false, dosLifespans: true);
        var i = P1Data.CreationTables.ForRules(false, false, dosLifespans: true);
        Assert.Equal(i.LifespanFactorsByRace, st.LifespanFactorsByRace);
        Assert.Equal((19, 1), st.LifespanFactorsByRace![(int)Race.Elf]);

        var session = NewSession(seed: 7);
        session.StartNewGame(Manual);
        session.OpenGuild();
        var human = session.CreateAndEnroll("Bands", Race.Human, CharacterClass.Fighter);
        Assert.InRange(human.Lifespan, 26 * 72, 26 * 88); // the manual's human band ±10 %, in 2-week units

        session.StartNewGame(St);
        session.OpenGuild();
        var stHuman = session.CreateAndEnroll("Factor", Race.Human, CharacterClass.Fighter);
        Assert.Equal(0, stHuman.Lifespan % Aging.DosLifespanStep); // @252 = @10 × 1, in @264 >> 8 steps
        Assert.InRange(stHuman.Lifespan / Aging.DosLifespanStep, 8, 14);
    }

    [OriginalDataFact]
    public void D2021_StAging_ComparesAgeClockShifted8_WithTheFactorLifespan()
    {
        // ST town_aging 0x0bba6 = DOS: @264 >> 8 above @252 → "has died". Same clock under the manual is far from the band.
        var m = new Character { Name = "OLD", Status = CharacterStatus.Ok, HpMax = 10, HpCur = 10, Lifespan = 10 * 256, AgeClock = 11 * 256 };
        var party = new Party();
        party.Members.Add(m);
        Aging.ApplyTownEntry(party, 1, dosLifespans: St.OriginalLifespans);
        Assert.Equal(CharacterStatus.Dead, m.Status);
    }

    // ==== helpers ===================================================================================================

    private sealed class StandInDeath : IMemberDeathRule
    {
        public void ApplyDeath(Character member) => member.Status = CharacterStatus.Dead;
    }

    /// <summary>A bare II campaign: only what the shared loop needs to build a session (the real one is not here).</summary>
    private sealed class StandInP2Campaign : ICampaign
    {
        public GameId Game => GameId.P2;
        public FeatureModuleSet DefaultFeatureModules { get; } = FeatureModuleSet.ForGame(GameId.P2);
        public IMemberDeathRule Death { get; } = new StandInDeath();
        public int StartTownId => 1;
        public IReadOnlyList<int> TransportTowns { get; } = [1];
        public bool IsUnnamedTransportTown(int townId) => false;
        public IReadOnlyDictionary<int, int> OverworldTownAliases { get; } = new Dictionary<int, int>();
        public string BlockedMessage(int regionId) => "BLOCKED";
        public IReadOnlyList<int> FallbackMonsterPool { get; } = [8];
        public bool MayDropItem(int itemId, int? dungeonLevelId) => true;
        public bool WithholdsDungeonTreasure(int firstMonsterId) => false;
        public void OnSpecialSite(GameSession session, OverworldMoveResult result) { }
        public void BeginDungeonEvent(GameSession session, DungeonMoveResult move) { }
        public void AnswerDungeonEvent(GameSession session, int choice) { }
        public void ResumeDungeonScript(GameSession session) { }
        public bool IsBlackKnightSpot(int dungeonLevelId) => false;
        public TownTransactionResult ConsultMystic(GameSession session) => new() { Kind = TownTransactionKind.Ok, Message = "" };
        public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result) { }
        public PartyDefeatResult ResolvePartyDefeat(GameSession session) => new() { Log = [] };
    }

    /// <summary>
    /// A session with II's rules profile, creation tables, overworld and monsters. Dungeons, items and towns are I's
    /// (II's loaders for them belong to the campaign work); none of the rules tested here reads them.
    /// </summary>
    private static GameSession NewSession(int seed = 4) => new(
        overworld: P2.CreateTraversal(),
        dungeons: P1Data.CreateDungeonTraversal(),
        items: P1Data.CreateItemCatalog(),
        townFactory: P1Data.CreateTownState,
        monsterRecord: id => P2.Monsters[id].ToRecord(),
        creation: P2.CreationTables,
        rng: new SeededDosRandomSource(seed),
        rules: P2.Rules,
        campaign: new StandInP2Campaign());

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

    private static Character Caster(int knownSpell, int level) => new()
    {
        Name = "MAGE", Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = level,
        Stats = new CharacterStats(10, 18, 10, 10, 10, 10), HpCur = 20, HpMax = 20,
        MagicCur = 10, MagicMax = 10, MpCeiling = 10,
        SpellsKnown = Enumerable.Range(0, SpellId.Count).Select(i => i == knownSpell ? 1 : 0).ToArray(),
    };

    private static CombatEncounter Fight(GameOptions rules, Character caster, params Monster[][] ranks)
    {
        var party = new Party();
        party.Members.Add(caster);
        var enc = new CombatEncounter(party) { Rules = rules, Modules = FeatureModuleSet.ForGame(GameId.P2) };
        foreach (var r in ranks)
            enc.AddRank(r);
        enc.Prepare();
        return enc;
    }
}
