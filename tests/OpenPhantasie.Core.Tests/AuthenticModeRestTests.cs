using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// PLAN P3.2 Authentic mode rest: DOS sides that are more than one branch (DOS data tables and Guild size per save, the
/// multi-step combat and town flows, the Client lists). Each is a <c>manual</c> / <c>dos</c> rules option; each test pins
/// both values, and the manual value is today's rule.
/// </summary>
public partial class AuthenticModeRestTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P1);

    private static GameOptions Dos(RuleOptionDescriptor d) => Manual.With(d.Id, "dos");

    private static GameSession NewGame(GameOptions options, int seed = 3)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        s.StartNewGame(options);
        return s;
    }

    private static byte[] Twns => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "master", "TWNS.INT"));

    private static int U16(ReadOnlySpan<byte> r, int offset) => r[offset] | r[offset + 1] << 8;

    /// <summary>Hands out exactly the scripted values (0..99 rolls and DOS words); any extra draw fails the test.</summary>
    private sealed class Strict(params int[] values) : IDosRandomSource
    {
        private readonly Queue<int> _q = new(values);

        public int Remaining => _q.Count;

        public int Next(int minInclusive, int maxExclusive)
        {
            Assert.NotEmpty(_q);
            return _q.Dequeue();
        }

        public int NextDosWord() => Next(0, 32768);
    }

    private static Character Member(string name, int spell = -1, int level = 1) => new()
    {
        Name = name, Class = CharacterClass.Wizard, Status = CharacterStatus.Ok, Level = level,
        Stats = new CharacterStats(10, 18, 10, 10, 10, 10), HpCur = 20, HpMax = 20,
        MagicCur = 30, MagicMax = 30, MpCeiling = 30,
        SpellsKnown = Enumerable.Range(0, SpellId.Count).Select(i => i == spell ? 1 : 0).ToArray(),
    };

    private static CombatEncounter Fight(GameOptions rules, int level, params Character[] members)
    {
        var party = new Party();
        foreach (var m in members)
            party.Members.Add(m);
        var enc = new CombatEncounter(party) { Rules = rules, MonsterLevel = level };
        enc.AddRank(new Monster { Name = "ORC", HitPoints = 20, Level = level, ExperienceValue = 10 },
            new Monster { Name = "ORC", HitPoints = 20, Level = level, ExperienceValue = 10 });
        enc.Prepare();
        return enc;
    }

    // ---- registry -------------------------------------------------------------------------------------------------

    public static TheoryData<string, string> Switches => new()
    {
        { "p1.halfling-dex", "D-001" }, { "p1.guild-size", "D-002" }, { "p1.lifespans", "D-003" },
        { "p1.monk-sleep", "D-006" }, { "p1.town-healing", "D-010" }, { "p1.beg-mercy", "D-017" },
        { "p1.sack-size", "D-018" }, { "p1.sleep", "D-020" }, { "p1.charm", "D-022" }, { "p1.teleport", "D-023" },
        { "p1.wild-treasure", "D-031" }, { "p1.summon-party", "D-033" }, { "p1.combat-shares", "D-053" },
        { "p1.continue", "D-069" }, { "p1.cast-list", "D-035" }, { "p1.transport-list", "D-062" },
        { "p1.combat-spell-list", "D-063" }, { "p1.orders-reset", "D-078" },
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

    // ---- DOS data tables (D-001, D-003, D-006) ----------------------------------------------------------------------

    [OriginalDataFact]
    public void DosTables_AreTwnsInt_D001_D006()
    {
        var twns = Twns;
        var dos = Data.CreationTables.ForRules(dosRaceModifiers: true, dosSpellLevels: true, dosLifespans: false);
        for (var r = 0; r < 15; r++)
            for (var a = 0; a < 5; a++)
                Assert.Equal(twns[0x942 + r * 5 + a], dos.RaceAttributeModifiers[r][a]);
        for (var c = 0; c < 6; c++)
            for (var s = 0; s < dos.SpellLevelsByClass[c].Count; s++)
                Assert.Equal(twns[0x5a + c * 60 + s], dos.SpellLevelsByClass[c][s]);
        // The loaded tables are not changed.
        Assert.Equal(12, Data.CreationTables.RaceAttributeModifiers[(int)Race.Halfling][2]);
        Assert.Equal(0, Data.CreationTables.SpellLevelsByClass[(int)CharacterClass.Monk][SpellId.Sleep]);
        Assert.Same(Data.CreationTables, Data.CreationTables.ForRules(false, false, false));
    }

    [OriginalDataFact]
    public void HalflingDexMaximum_FollowsTheSave_D001()
    {
        var halfling = new Character { Race = Race.Halfling, BirthRace = Race.Halfling };
        Assert.Equal(20, MagicalPool.RaceMaximum(halfling, 2, NewGame(Manual).CreationTables));
        Assert.Equal(19, MagicalPool.RaceMaximum(halfling, 2, NewGame(Dos(RuleOptionRegistry.HalflingDex)).CreationTables));
    }

    [OriginalDataFact]
    public void MonkLearnsSleepAtLevel7_OnlyUnderDos_D006()
    {
        foreach (var dos in new[] { false, true })
        {
            var s = NewGame(dos ? Dos(RuleOptionRegistry.MonkSleep) : Manual);
            var monk = new Character { Class = CharacterClass.Monk, Stats = new CharacterStats(10, 10, 10, 10, 10, 10), SpellsKnown = new int[SpellId.Count] };
            LevelUp.Apply(monk, 6, s.CreationTables, new SeededDosRandomSource(1));
            Assert.Equal(7, monk.Level);
            Assert.Equal(dos ? 2 : 0, monk.SpellsKnown[SpellId.Sleep]);
        }
    }

    [OriginalDataTheory]
    [InlineData("GUILD.roster22.dat")]
    [InlineData("GUILD.pass4.dat")]
    public void DosLifespanFactors_ReproduceTheFixtureRecords_D003(string fixture)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        var dos = Data.CreationTables.ForRules(false, false, dosLifespans: true);
        var checkedRecords = 0;
        for (var i = 0; i < GuildCodec.SlotCount; i++)
        {
            var r = bytes.AsSpan(i * GuildCodec.RecordSize, GuildCodec.RecordSize);
            if (U16(r, 92) == 0)
                continue;
            var race = (Race)U16(r, 216);
            var seed = U16(r, 10);
            Assert.Equal(U16(r, 252) * Aging.DosLifespanStep, CharacterCreation.LifespanFor(race, seed, dos));
            checkedRecords++;
        }
        Assert.True(checkedRecords >= 6);
    }

    [OriginalDataFact]
    public void NewMember_GetsTheSavesLifespan_D003()
    {
        foreach (var dos in new[] { false, true })
        {
            var s = NewGame(dos ? Dos(RuleOptionRegistry.Lifespans) : Manual);
            s.OpenGuild();
            var elf = s.CreateAndEnroll("Elfo", Race.Elf, CharacterClass.Wizard);
            var seed = elf.Stats.Luck;
            if (dos)
                Assert.Equal(seed * 19 * 256, elf.Lifespan);
            else
                Assert.Equal(CharacterCreation.LifespanFor(Race.Elf, seed, Data.CreationTables), elf.Lifespan);
            Assert.InRange(elf.AgeClock, 260 + elf.Lifespan / 10, 260 + elf.Lifespan / 8 + 1); // DOS starting age either way
        }
    }

    [Theory]
    [InlineData(false, 13 * 256 + 1, "died")] // manual: past the lifespan in clock units
    [InlineData(true, 13 * 256 + 1, "aged")]  // DOS: @264 >> 8 = 13, not above @252 = 13
    [InlineData(true, 14 * 256 - 1, "aged")]
    [InlineData(true, 14 * 256, "died")]      // 14 > 13
    [InlineData(true, 10 * 256 - 1, "none")]  // 9 ≤ 13·7/10 = 9
    [InlineData(true, 10 * 256, "aged")]
    [InlineData(false, 13 * 256 * 7 / 10 + 1, "aged")]
    public void Aging_ComparesInDosSteps_D003(bool dos, int ageClock, string expected)
    {
        var m = new Character
        {
            Name = "OLD", Status = CharacterStatus.Ok, Lifespan = 13 * 256, AgeClock = ageClock,
            Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        };
        var party = new Party();
        party.Members.Add(m);
        var lines = Aging.ApplyTownEntry(party, 1, dosLifespans: dos);
        var got = m.Status == CharacterStatus.Dead ? "died" : m.HasAged ? "aged" : "none";
        Assert.Equal(expected, got);
        Assert.Equal(expected == "none" ? 0 : 1, lines.Count);
    }

    [OriginalDataFact]
    public void DosImport_KeepsThe252Lifespan_UnderDos_D003()
    {
        var guild = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GUILD.roster22.dat"));
        foreach (var dos in new[] { false, true })
        {
            var s = NewGame(dos ? Dos(RuleOptionRegistry.Lifespans) : Manual);
            DosSaveConverter.ImportInto(s, new DosSaveSet { Guild = guild }, Data);
            var elf = s.Roster.Slots[0]!;
            Assert.Equal(Race.Elf, elf.BirthRace);
            if (dos)
                Assert.Equal(171 * 256, elf.Lifespan);
            else
                Assert.Equal(CharacterCreation.LifespanFor(Race.Elf, 9, Data.CreationTables), elf.Lifespan);
            // Export writes the DOS @252 back unchanged either way (the manual band was not changed since import).
            var export = DosSaveConverter.Export(s.CaptureSnapshot());
            Assert.Equal(171, U16(export.Files!.Guild, 252));
        }
    }

    // ---- Guild size (D-002) ---------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, 21)]
    [InlineData(true, 22)]
    public void GuildFills_TheSavesSize_D002(bool dos, int max)
    {
        var s = NewGame(dos ? Dos(RuleOptionRegistry.GuildSize) : Manual);
        Assert.Equal(max, s.Roster.MaxCharacters);
        s.OpenGuild();
        for (var i = 0; i < max; i++)
            s.CreateAndEnroll("M" + i, Race.Human, CharacterClass.Fighter);
        Assert.False(s.GuildHasRoom);
        Assert.Equal(max, s.Roster.Occupied.Count());
    }

    // ---- town HEALING (D-010) -------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, CastLocation.Town, 0, 1)]       // manual: (10·0)/100 floored to 1
    [InlineData(true, CastLocation.Town, 0, 0)]        // DOS town: rand / (32767/10), no floor
    [InlineData(true, CastLocation.Town, 32767, 10)]   // the top word reaches c
    [InlineData(true, CastLocation.Town, 16000, 4)]    // 16000 / 3276
    [InlineData(true, CastLocation.Field, 0, 1)]       // the field routine has the floor in DOS too
    public void TownHealing_DosRange_D010(bool dos, CastLocation where, int roll, int healed)
    {
        var caster = Member("PRIEST", SpellId.HealingI);
        caster.HpCur = 5;
        var party = new Party();
        party.Members.Add(caster);
        var rng = new Strict(roll);
        var r = SpellCaster.CastOutOfCombat(party, caster, SpellId.HealingI, caster, rng, where,
            rules: dos ? Dos(RuleOptionRegistry.TownHealing) : Manual);
        Assert.Equal(healed, r.HpRestored);
        Assert.Equal(0, rng.Remaining);
    }

    // ---- BEG FOR MERCY (D-017) ------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(89, PartyOptionOutcome.CombatOver)]
    [InlineData(90, PartyOptionOutcome.GiveOrders)] // rand < Σreaction + 90 fails: `core.combat.monsters_dont_respond`
    public void BegForMercy_CanFail_UnderDos_D017(int roll, PartyOptionOutcome outcome)
    {
        var enc = Fight(Dos(RuleOptionRegistry.BegMercy), 1, Member("A"));
        var r = PartyOptions.Choose(enc, PartyOption.BegMercy, new Strict(roll), inDungeon: false, terrainClass: 1);
        Assert.Equal(outcome, r.Outcome);
        Assert.Equal(outcome == PartyOptionOutcome.CombatOver, r.ForfeitsSack);

        var manual = PartyOptions.Choose(Fight(Manual, 1, Member("A")), PartyOption.BegMercy, new Strict(), false, 1);
        Assert.Equal(PartyOptionOutcome.CombatOver, manual.Outcome); // no roll drawn
    }

    // ---- found-item capacity (D-018) ------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, 90)]
    [InlineData(true, 60)]
    public void SackCapacity_FollowsTheSave_D018(bool dos, int capacity)
    {
        var s = NewGame(dos ? Dos(RuleOptionRegistry.SackSize) : Manual);
        Assert.Equal(capacity, s.Sack.Capacity);
        for (var i = 0; i < capacity; i++)
            Assert.True(s.Sack.TryAdd(1 + i % 50));
        Assert.True(s.Sack.IsFull);
        Assert.False(s.Sack.TryAdd(7));
    }

    // ---- SLEEP (D-020) --------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Sleep_Dos_PerMonsterLSquared_RestOfTheRound_D020()
    {
        var enc = Fight(Dos(RuleOptionRegistry.Sleep), 3, Member("MAGE", SpellId.Sleep, level: 5));
        var rng = new Strict(0, 8, 0, 9); // per monster: a discarded draw, then r; L² = 9
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Sleep), rng, []);
        Assert.Equal(0, rng.Remaining);
        var mons = enc.AllMonsters.ToList();
        Assert.False(mons[0].DoneThisRound);
        Assert.True(mons[1].DoneThisRound);
        Assert.All(mons, m => Assert.Equal(MonsterStatusFlags.None, m.StatusFlags & MonsterStatusFlags.Asleep));
    }

    // ---- CHARM (D-022) --------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Charm_Dos_AddsReaction_AndTheFightGoesOn_D022(bool dos)
    {
        var enc = Fight(dos ? Dos(RuleOptionRegistry.Charm) : Manual, 1, Member("MAGE", SpellId.Charm, level: 3));
        enc.MonsterDamaged = true;
        var before = enc.ReactionSum;
        Assert.True(CombatSpells.Charm(enc, enc.Party.Members[0], new Strict(50, 10), []));
        if (dos)
        {
            Assert.Equal(SpellCombatEnd.None, enc.SpellEnd);
            Assert.Equal(before + CombatSpells.CharmReactionBonus, enc.ReactionSum);
            Assert.False(enc.MonsterDamaged); // GREETINGS works again
        }
        else
        {
            Assert.Equal(SpellCombatEnd.Charmed, enc.SpellEnd);
        }
    }

    // ---- TELEPORTATION (D-023) ------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(52, true)]  // rand < 3·1 + 50
    [InlineData(53, false)]
    public void Teleport_Dos_CanFail_EndsAtTheRoundTop_D023(int roll, bool works)
    {
        var enc = Fight(Dos(RuleOptionRegistry.Teleport), 1, Member("MAGE", SpellId.Teleportation));
        enc.RewardGold = 500;
        var rng = new Strict(roll);
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Teleportation), rng, []);
        Assert.Equal(0, rng.Remaining);
        Assert.Equal(works ? SpellCombatEnd.TeleportedAtRoundTop : SpellCombatEnd.None, enc.SpellEnd);
        Assert.False(enc.SpellEnd.EndsAtOnce()); // the round plays out
        if (!works)
            return;
        var end = CombatSpells.EndBySpell(enc);
        Assert.Equal(0, end.RewardGold);
        Assert.True(enc.TreasureForfeited);
    }

    [OriginalDataFact]
    public void Teleport_Manual_NoRoll_AtOnce_D023()
    {
        var enc = Fight(Manual, 1, Member("MAGE", SpellId.Teleportation));
        SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Teleportation), new Strict(), []);
        Assert.Equal(SpellCombatEnd.Teleported, enc.SpellEnd);
    }

    // ---- wilderness treasure (D-031) ------------------------------------------------------------------------------

    private static CombatEncounter Loot(GameOptions rules, int[] treasure, int level = 2)
    {
        var party = new Party();
        party.Members.Add(Member("A"));
        var enc = new CombatEncounter(party) { Rules = rules, TreasureItemIds = treasure, MonsterLevel = level, FirstMonsterId = 10 };
        enc.AddRank(new Monster { Name = "M", HitPoints = 1 });
        return enc;
    }

    [OriginalDataTheory]
    [InlineData(24, true)]  // rand ≤ 2·2 + 20
    [InlineData(25, false)]
    public void WildernessTreasure_Dos_IsRolled_D031(int presence, bool drops)
    {
        var rng = new Strict(drops ? [presence, 0, 0] : [presence]);
        var offers = TreasureDrop.Roll(Loot(Dos(RuleOptionRegistry.WildernessTreasure), [50, 60]), null, 181, rng, []);
        Assert.Equal(drops ? new[] { 53, 63 } : [], offers);
        Assert.Equal(0, rng.Remaining);
    }

    [OriginalDataTheory]
    [InlineData(false, new int[0])]
    [InlineData(true, new[] { 137 })] // DOS tests the unshifted 135: FIRE ELEMENTAL's shifted SCROLL 17 drops anywhere
    public void Scroll17Guard_Dos_TestsTheUnshiftedId_D031(bool dos, int[] expected)
    {
        var rng = new Strict(dos ? [0, 0, 20] : [0, 20]); // (DOS presence), id 0's shift, 135 + 3 − 20/20 = 137
        var rules = dos ? Dos(RuleOptionRegistry.WildernessTreasure) : Manual;
        Assert.Equal(expected, TreasureDrop.Roll(Loot(rules, [0, 135]), null, 181, rng, []));
        Assert.Equal(0, rng.Remaining);
    }

    // ---- `core.spell.summon_elemental` party test (D-033) ----------------------------------------------------------------------

    private static CombatEncounter Party6WithElemental(GameOptions rules, int dead)
    {
        var party = new Party();
        party.Members.Add(Member("CASTER", SpellId.SummonElemental));
        for (var i = 1; i < Party.MaxMembers; i++)
            party.Members.Add(Member($"M{i}"));
        Assert.True(GuildHall.TryAddElemental(party, Data.Elementals[0].ToCharacter(), out _));
        for (var i = 0; i < dead; i++)
            party.Members[Party.MaxMembers - 1 - i].Status = CharacterStatus.Dead;
        var enc = new CombatEncounter(party) { Rules = rules, MonsterLevel = 1 };
        enc.AddRank(new Monster { Name = "ORC", HitPoints = 20, Level = 1, ExperienceValue = 10 });
        enc.Prepare();
        return enc;
    }

    [OriginalDataTheory]
    [InlineData(false, 0, false, true)]
    [InlineData(false, 0, true, true)]
    [InlineData(false, 1, true, false)]
    [InlineData(true, 0, false, true)]  // after orders: last living index 5 = six non-DEAD
    [InlineData(true, 0, true, false)]  // after a monster turn: count 6 ≠ 5
    [InlineData(true, 1, true, true)]   // … and five non-DEAD pass
    [InlineData(true, 1, false, false)]
    public void Summon_Dos_IndexOrCountTest_D033(bool dos, int dead, bool monsterActed, bool works)
    {
        var enc = Party6WithElemental(dos ? Dos(RuleOptionRegistry.SummonParty) : Manual, dead);
        enc.LivingWordIsCount = monsterActed;
        Assert.Equal(works, CombatSpells.SummonElemental(enc));
    }

    // ---- end-of-combat shares (D-053) -----------------------------------------------------------------------------

    [Theory]
    [InlineData(308, 100, 6, 50, 16, 300, 96)]  // s35: 4 SKELETON, 6 survivors → 50 each
    [InlineData(588, 7, 5, 110, 1, 550, 5)]     // s35: 4 BANTIR, 5 survivors → 110 each
    public void SurvivorShares_DropTheRemainders_D053(long xp, long gold, int n, long xpEach, long goldEach, long poolXp, long poolGold)
    {
        Assert.Equal((xpEach, goldEach, poolXp, poolGold), GameSession.SurvivorShares(xp, gold, n));
    }

    private static GameSession WildernessFight(GameOptions rules, params string[] names)
    {
        var s = NewGame(rules);
        s.OpenGuild();
        foreach (var n in names)
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(n, Race.Human, CharacterClass.Fighter), out _));
        s.DepartGuildToWilderness();
        s.BeginWildernessCombat(Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        return s;
    }

    [OriginalDataFact]
    public void SessionCast_NonCombatSpell_UnderDos_SpendsMp_D063()
    {
        var s = WildernessFight(Dos(RuleOptionRegistry.CombatSpellList), "A");
        var hero = s.Party.Members[0];
        hero.SpellsKnown[SpellId.Vision] = 1;
        hero.MagicCur = hero.MagicMax = 10;
        var packed = s.ChooseAction(0, CombatChoice.Cast, spellIndex: SpellId.Vision);
        Assert.Equal(CombatAction.CastSpell(SpellId.Vision), packed);
        var r = SpellCaster.ResolveCombatCast(s.Combat!, 0, packed, new Strict(), []);
        Assert.Equal(10 - Magic.MagicPointCost(SpellId.Vision), hero.MagicCur);
        Assert.True(r.Success);
    }

    [Fact]
    public void SurvivorShares_AfterBeg_KeepThePurseRemainder_D053()
    {
        Assert.Equal((0L, -92L, 0L, 4L), GameSession.SurvivorShares(0, 0, 6, begPurse: 556)); // DOSBox s65
    }

    // ---- Continue (D-069) -----------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Continue_Dos_LoadsTheSavedParty_WithoutAsking_D069(bool dos)
    {
        var s = NewGame(dos ? Dos(RuleOptionRegistry.ContinueParty) : Manual);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.LeaveGuild();
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        Assert.Equal(dos, GameSession.ContinuesWithoutAsking(file, GameId.P1));

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        loaded.ContinueSavedParty(file);
        Assert.Equal(["Hero"], loaded.Party.Members.Select(m => m.Name));
        Assert.Equal(GameMode.Town, loaded.Mode);
    }

    // ---- out-of-combat Cast list (D-035) --------------------------------------------------------------------------

    [OriginalDataFact]
    public void TownCast_Dos_ListsEverySpell_ACombatSpellSpendsMpForNothing_D035()
    {
        var caster = Member("MONK", SpellId.FireflashI);
        var party = new Party();
        party.Members.Add(caster);
        Assert.Empty(SpellCaster.OutOfCombatChoices(caster, CastLocation.Town, Manual));
        var dos = Dos(RuleOptionRegistry.CastList);
        Assert.Equal([SpellId.FireflashI], SpellCaster.OutOfCombatChoices(caster, CastLocation.Town, dos));

        var refused = SpellCaster.CastOutOfCombat(party, caster, SpellId.FireflashI, null, new Strict(), CastLocation.Town, rules: Manual);
        Assert.False(refused.Success);
        Assert.Equal(30, caster.MagicCur);

        var r = SpellCaster.CastOutOfCombat(party, caster, SpellId.FireflashI, null, new Strict(), CastLocation.Town, rules: dos);
        Assert.True(r.Success);
        Assert.Equal(Magic.MagicPointCost(SpellId.FireflashI), r.MpSpent);
        Assert.Equal(30 - r.MpSpent, caster.MagicCur);
        Assert.Equal("", r.Message);
    }

    [Theory]
    [InlineData(SpellId.Transportation, CastLocation.Town, true)]
    [InlineData(SpellId.Transportation, CastLocation.Field, false)]
    [InlineData(SpellId.Vision, CastLocation.Field, true)]
    [InlineData(SpellId.Vision, CastLocation.Dungeon, false)]
    [InlineData(SpellId.Vision, CastLocation.Town, false)]
    [InlineData(SpellId.MonsterEval, CastLocation.Dungeon, true)]
    [InlineData(SpellId.MonsterEval, CastLocation.Town, false)]
    [InlineData(SpellId.HealingIv, CastLocation.Town, true)]
    [InlineData(SpellId.Resurrection, CastLocation.Field, true)]
    [InlineData(SpellId.Charm, CastLocation.Field, false)]
    public void DosOutOfCombatSwitch_D035(int spell, CastLocation where, bool effect)
    {
        Assert.Equal(effect, SpellCaster.HasDosOutOfCombatEffect(spell, where));
    }

    // ---- TRANSPORTATION list (D-062) ------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransportDestinations_IncludeTheCurrentTown_UnderDos_D062(bool dos)
    {
        var s = NewGame(dos ? Dos(RuleOptionRegistry.TransportList) : Manual);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Equal(dos, s.TransportDestinations.Contains(1));
        Assert.Equal(dos ? 12 : 11, s.TransportDestinations.Count);
    }

    // ---- combat spell list (D-063) --------------------------------------------------------------------------------

    [OriginalDataFact]
    public void CombatPicker_Dos_NonCombatSpellSpendsMpForNothing_D063()
    {
        Assert.False(SpellCaster.IsCombatCastable(SpellId.Vision, Manual));
        var dos = Dos(RuleOptionRegistry.CombatSpellList);
        Assert.True(SpellCaster.IsCombatCastable(SpellId.Vision, dos));

        var enc = Fight(dos, 1, Member("MAGE", SpellId.Vision));
        var log = new List<string>();
        var r = SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(SpellId.Vision), new Strict(), log);
        Assert.True(r.Success);
        Assert.Equal(Magic.MagicPointCost(SpellId.Vision), r.MpSpent);
        Assert.Single(log); // "%s CASTS A SPELL" only
    }

    // ---- combat orders reset (D-078) ------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void LeavingTown_CountsAnOrdersReset_UnderDos_D078(bool dos, int resets)
    {
        var s = NewGame(dos ? Dos(RuleOptionRegistry.OrdersReset) : Manual);
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.LeaveGuild();
        Assert.True(s.LeaveTown());
        Assert.Equal(resets, s.OrdersResets);
    }
}
