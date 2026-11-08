using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// PLAN P3.2 Authentic mode: the DOS behaviours the Original (DOS) preset reaches through rules options — D-015 (STUNNING
/// BLOW on a just-killed member), D-016 (the armor-reduced special) and Q2 (the payout remainder). Each is pinned for its
/// preset values and for both behaviours; the manual value keeps today's play.
/// </summary>
public class AuthenticModeTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>Yields a fixed sequence of Next() results, then repeats the last.</summary>
    private sealed class ScriptedRandomSource(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count == 0)
                return minInclusive;
            var v = _values.Dequeue();
            if (_values.Count == 0)
                _values.Enqueue(v);
            return v;
        }
    }

    private static GameOptions With(string id, string value) => GameOptions.ForPreset(GameId.P1).With(id, value);

    private static Character Member(string name, int hp = 40) => new()
    {
        Name = name,
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(10, 10, 10, 10, 10, 10),
        Skills = new CharacterSkills(80, 0, 0, 0, 0, 0, 0, 0),
        HpCur = hp,
        HpMax = hp,
        MagicMax = 10,
        MagicCur = 10,
        WeaponDamageA = 20,
    };

    private static Monster Caster(params int[] specials) => new()
    {
        Name = "CASTER",
        HitPoints = 10,
        CombatRating = 10,
        SpecialChance = 100,
        Specials = specials,
        AttackPower = 82,
        DamageField = 15,
    };

    private static CombatEncounter Encounter(GameOptions rules, Monster mon, params Character[] members)
    {
        var party = new Party();
        party.Members.AddRange(members);
        var enc = new CombatEncounter(party) { Rules = rules };
        enc.AddRank(mon);
        enc.Prepare();
        return enc;
    }

    // ---- descriptors and presets ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("p1.stunning-blow", "manual", "dos", "manual", "D-015")]
    [InlineData("p1.armor-reduced", "armor", "weapon", "armor", "D-016")]
    [InlineData("p1.payout-remainder", "first-survivor", "dropped", "first-survivor", "D-037")]
    public void Descriptor_PresetValues(string id, string manual, string dos, string remaster, string difference)
    {
        var d = RuleOptionRegistry.For(GameId.P1).Single(o => o.Id == id);
        Assert.Equal((manual, dos, remaster, difference), (d.Manual, d.Dos, d.Remaster, d.Difference));
        Assert.Equal(manual, d.Values[0]); // the manual's value is listed first
    }

    [Fact]
    public void Presets_GiveTheTypedRules()
    {
        var manual = GameOptions.ForPreset(GameId.P1);
        Assert.False(manual.DosStunningBlow);
        Assert.False(manual.DosArmorReduced);
        Assert.False(manual.PayoutRemainderDropped);

        var dos = GameOptions.ForPreset(GameId.P1, OptionPreset.Dos);
        Assert.True(dos.DosStunningBlow);
        Assert.True(dos.DosArmorReduced);
        Assert.True(dos.PayoutRemainderDropped);

        var remaster = GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster);
        Assert.False(remaster.DosStunningBlow);
        Assert.False(remaster.DosArmorReduced);
        Assert.False(remaster.PayoutRemainderDropped);
    }

    [Fact]
    public void A_bare_encounter_plays_the_manual_rules()
    {
        var enc = new CombatEncounter(new Party());
        Assert.Equal(OptionPreset.Manual, enc.Rules.Preset);
        Assert.Throws<ArgumentNullException>(() => enc.Rules = null!);
    }

    [OriginalDataFact]
    public void The_options_are_locked_into_the_save_and_reach_a_restored_fight()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var other = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        other.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.True(other.Options.DosStunningBlow);
        Assert.True(other.Options.DosArmorReduced);
        Assert.True(other.Options.PayoutRemainderDropped);
    }

    [OriginalDataFact]
    public void A_new_fight_gets_the_saves_rules()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(With("p1.stunning-blow", "dos"));
        s.OpenGuild();
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.DepartGuildToWilderness();
        s.BeginWildernessCombat(Data.Monsters.First(m => m.HpBase <= 8).Id);
        Assert.Same(s.Options, s.Combat!.Rules);

        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        var other = GameSessionFactory.Create(Data, new SeededDosRandomSource(4));
        other.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.NotNull(other.Combat);
        Assert.True(other.Combat!.Rules.DosStunningBlow); // a mid-fight save keeps playing its rules
    }

    // ---- D-015: STUNNING BLOW on a just-killed member -------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(false, CharacterStatus.Dead)]
    [InlineData(true, CharacterStatus.Sleep)]
    public void StunningBlow_OnTheDead_FollowsTheRule_D015(bool dos, CharacterStatus expected)
    {
        var a = Member("A");
        a.Status = CharacterStatus.Dead;
        a.HpCur = 0;
        var prep = new CombatantPrep { PackedAction = 3 };
        var log = new List<string>();
        MonsterSpecials.ApplyRider(MonsterMeleeRider.StunningBlow, a, prep, log, stunTheDead: dos);
        Assert.Equal(expected, a.Status);
        Assert.Equal(dos, log.Contains("A STUNNING BLOW"));
        Assert.Equal(dos ? CombatAction.Cleared : 3, prep.PackedAction);
    }

    [Theory]
    [InlineData("manual", CharacterStatus.Dead)]
    [InlineData("dos", CharacterStatus.Sleep)]
    public void StunningBlow_ThatKills_DosLeavesTheCorpseAsleep_AndItCanDieAgain_D015(string value, CharacterStatus after)
    {
        var wight = Caster(34, 34, 34);
        var hero = Member("HERO", hp: 5);
        var other = Member("OTHER");
        var enc = Encounter(With("p1.stunning-blow", value), wight, hero, other);
        var con = hero.Stats.Con;

        // monster ready 0, gate 0, pick 0 → 34, target 0 (HERO), hit 0, damage 50 (7 ≥ 5 HP), then 99s
        new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0, 0, 0, 0, 0, 50, 99));

        Assert.Equal(after, hero.Status);
        Assert.Equal(0, hero.HpCur);
        var conAfterFirst = hero.Stats.Con;
        Assert.True(conAfterFirst < con); // D-009: CON lost at the first death either way
        Assert.False(enc.PartyWiped);

        if (value != "dos")
            return;
        // DOS: the SLEEP corpse is in the living list and never wakes (HP 0); the next blow kills it again.
        Assert.Contains(hero, enc.Party.Members.Where(m => m.Status != CharacterStatus.Dead));
        new CombatResolver().ResolveRound(enc, new ScriptedRandomSource(0, 0, 0, 0, 0, 50, 99));
        Assert.Equal(CharacterStatus.Sleep, hero.Status); // killed again, and stunned again
        Assert.True(hero.Stats.Con < conAfterFirst);       // a second CON loss
    }

    // ---- D-016: `core.combat.armor_reduced` --------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData("armor", 4, 20)]
    [InlineData("weapon", 10, 8)]
    public void ArmorReduced_ScalesArmorOrWeaponDamage_D016(string value, int armor, int weapon)
    {
        var mon = Caster(17, 17, 17); // power 8
        var a = Member("A");
        var enc = Encounter(With("p1.armor-reduced", value), mon, a);
        enc.Prep[0].ArmorEff = 10;
        Assert.Equal(20, enc.Prep[0].WeaponDamageEff);
        var log = new List<string>();

        // pick 0; scaled 50·8/100 = 4; resist 0 → affected; field (f·4 + 5)/10: armor 10 → 4, weapon 20 → 8
        MonsterSpecials.Use(enc, enc.Ranks[0], mon, new ScriptedRandomSource(0, 50, 0), log);

        Assert.Equal(armor, enc.Prep[0].ArmorEff);
        Assert.Equal(weapon, enc.Prep[0].WeaponDamageEff);
        Assert.Contains("THE PARTY'S ARMOR IS REDUCED", log); // the same message either way
    }

    // ---- Q2: the town-entry payout remainder ----------------------------------------------------------------------

    [Theory]
    [InlineData(false, 34L, 33L, 34L, 33L, 100L)]
    [InlineData(true, 33L, 33L, 33L, 33L, 99L)]
    public void Payout_Remainder_GoesToTheFirstSurvivor_OrIsLost_Q2(
        bool drop, long firstXp, long secondXp, long firstGold, long secondGold, long goldDelta)
    {
        var a = Member("A");
        var b = Member("B");
        var c = Member("C");
        var party = new Party();
        party.Members.AddRange([a, b, c]);
        var sack = new LootSack { Experience = 100, Gold = 100 };

        var r = TownMenus.Distribute(party, sack, dropRemainder: drop);

        Assert.Equal((firstXp, secondXp, secondXp), (a.Experience, b.Experience, c.Experience));
        Assert.Equal(firstGold + secondGold * 2, party.GoldCarried);
        Assert.Equal(goldDelta, r.GoldDelta);
        Assert.Equal(!drop, r.Message.Contains("remainder"));
        Assert.Equal(0, sack.Gold);
        Assert.Equal(0, sack.Experience);
    }

    [OriginalDataTheory]
    [InlineData("first-survivor", 100L)]
    [InlineData("dropped", 99L)]
    public void Payout_InTown_FollowsTheSavesRule_Q2(string value, long purse)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame(With("p1.payout-remainder", value));
        s.OpenGuild();
        foreach (var name in new[] { "Ann", "Bob", "Cid" })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, CharacterClass.Fighter), out _));
        s.LeaveGuild();
        s.Party.GoldCarried = 0;
        s.Sack.DepositRewards(100, 100);

        s.DistributeLoot();

        Assert.Equal(purse, s.Party.GoldCarried);
        Assert.Equal(purse == 100 ? 34 : 33, s.Party.Members[0].Experience);
    }
}
