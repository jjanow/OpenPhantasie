using System.Security.Cryptography;
using System.Text;
using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Golden combat trace (Phase 09 T1.6): fixed-seed fights covering party melee (all swing counts, thief ranks), a combat
/// cast, PARRY, monster melee, monster specials (single target, party blast, sleep, stunning-blow rider) and kills.
/// Every RNG call (range + result) and every log line is hashed. The goldens were recorded on the code before the
/// feature-module seam, so a match proves the seam kept the RNG call order and the output identical.
/// </summary>
public class CombatTraceGoldenTests
{
    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly SeededRandomSource _inner = new(seed);
        public StringBuilder Trace { get; } = new();

        public int Next(int minInclusive, int maxExclusive)
        {
            var v = _inner.Next(minInclusive, maxExclusive);
            Trace.Append($"r{minInclusive},{maxExclusive}={v};");
            return v;
        }
    }

    private static Character Member(string name, CharacterClass cls, int level, int hp, int attack, int parry) => new()
    {
        Name = name,
        Class = cls,
        Level = level,
        Status = CharacterStatus.Ok,
        Stats = new CharacterStats(14, 16, 12, 13, 11, 9),
        Skills = new CharacterSkills(attack, parry, 0, 0, 0, 0, 0, 0),
        HpCur = hp,
        HpMax = hp,
        MagicMax = 30,
        MagicCur = 30,
        WeaponDamageA = 12,
        WeaponDamageB = 8,
        ArmorSoak = 3,
        ShieldSoak = 3,
    };

    private static Monster Mon(string name, int hp, int attack, int dmg, int armor, int defense,
        int specialChance = 0, params int[] specials) => new()
    {
        Name = name,
        HitPoints = hp,
        AttackPower = attack,
        DamageField = dmg,
        Armor = armor,
        Defense = defense,
        CombatRating = 10,
        ExperienceValue = 7,
        SpecialChance = specialChance,
        Specials = specials,
    };

    /// <summary>Runs up to 12 rounds; returns the first 128 bits of the trace SHA-256 (hex).</summary>
    internal static string RunFight(int seed, Action<CombatEncounter>? configure = null)
    {
        var fighter = Member("FIGHTER", CharacterClass.Fighter, 4, 80, 90, 30);
        var thief = Member("THIEF", CharacterClass.Thief, 3, 40, 55, 20);
        var wizard = Member("WIZARD", CharacterClass.Wizard, 3, 30, 30, 10);
        wizard.SpellsKnown[SpellId.FireflashI] = 1;
        var monk = Member("MONK", CharacterClass.Monk, 5, 50, 80, 60);
        var party = new Party();
        party.Members.AddRange([fighter, thief, wizard, monk]);

        var enc = new CombatEncounter(party);
        enc.AddRank(Mon("ORC", 9, 82, 10, 1, 10), Mon("ORC", 9, 82, 10, 1, 10), Mon("ORC", 9, 82, 10, 1, 10));
        enc.AddRank(Mon("SHAMAN", 24, 82, 8, 1, 10, 90, 2, 25, 34), Mon("SHAMAN", 24, 82, 8, 1, 10, 90, 33, 3, 35));
        enc.AddRank(Mon("ARCHER", 15, 82, 9, 1, 10));
        enc.MonsterLevel = 6;
        enc.Prepare();
        configure?.Invoke(enc);

        var rng = new RecordingRandom(seed);
        var trace = rng.Trace;
        for (var round = 0; round < 12; round++)
        {
            enc.EnsurePrep();
            var orders = round % 3;
            enc.SetAction(0, orders == 0 ? CombatAction.Attack(0) : CombatAction.SlashFor(enc.Prep[0].WeaponHitEff));
            enc.SetAction(1, CombatAction.Thrust(Math.Min(round % 3, enc.Ranks.Count - 1)));
            enc.SetAction(2, round % 2 == 0 ? CombatAction.CastSpell(SpellId.FireflashI) : CombatAction.Thrust(0));
            enc.SetAction(3, orders == 2 ? CombatAction.Parry : CombatAction.Lunge());

            var result = new CombatResolver().ResolveRound(enc, rng);
            trace.Append($"|R{round}:");
            foreach (var line in result.Log)
                trace.Append(line).Append('/');
            foreach (var m in party.Members)
                trace.Append($"{m.Name}:{m.HpCur}:{m.Status}:{m.MagicCur};");
            foreach (var m in enc.AllMonsters)
                trace.Append($"{m.Name}:{m.HitPoints};");
            trace.Append($"xp{enc.RewardXp}");
            if (result.EncounterOver)
            {
                trace.Append($"|END:{result.PartyWon}:{result.PartyWiped}:{result.RewardXp}:{result.RewardGold}");
                break;
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString())))[..32];
    }

    [OriginalDataTheory]
    [InlineData(1, "8A7486E54ACD88889F16C32F1E78F964")]
    [InlineData(77, "E64B539150E0B023F9A232399181693E")]
    [InlineData(2026, "48933036C6B7CBF2CBC90D0086BB2056")]
    public void FightTrace_MatchesPreSeamGolden(int seed, string expected) =>
        Assert.Equal(expected, RunFight(seed));
}
