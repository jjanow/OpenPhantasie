using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s133: a party damage spell's hit carries the amount the spell rolled (<see cref="CombatHit.SpellDamage"/>, before a
/// monster's armour and the floor) for I Amiga's sparkle (h53 <c>+0xab8</c>); presentation only.
/// </summary>
public class SpellDamageTagTests
{
    private static (CombatEncounter Enc, Monster[] Mons) Fight(int spell, int armor)
    {
        var party = new Party();
        party.Members.Add(new Character
        {
            Name = "WIZARD", Class = CharacterClass.Wizard, Level = 5, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(14, 16, 12, 13, 11, 9),
            HpCur = 30, HpMax = 30, MagicMax = 60, MagicCur = 60,
            SpellsKnown = { [spell] = 1 },
        });
        var enc = new CombatEncounter(party);
        Monster Orc() => new()
        {
            Name = "ORC", HitPoints = 200, AttackPower = 50, DamageField = 5, Armor = armor, Defense = 10, CombatRating = 10,
            ExperienceValue = 7,
        };
        var mons = new[] { Orc(), Orc(), Orc() };
        enc.AddRank(mons);
        enc.Prepare();
        return (enc, mons);
    }

    [Theory]
    [InlineData(SpellId.FireflashIv, 0)]
    [InlineData(SpellId.MindblastIv, 0)]
    [InlineData(SpellId.FlameboltIv, 3)]
    public void DamageSpells_TagTheRolledAmount(int spell, int armor)
    {
        foreach (var seed in new[] { 1, 9, 77, 2026 })
        {
            var (enc, mons) = Fight(spell, armor);
            enc.TakeRoundHits();
            SpellCaster.ResolveCombatCast(enc, 0, CombatAction.CastSpell(spell), new SeededRandomSource(seed), []);
            var hits = enc.TakeRoundHits();
            Assert.NotEmpty(hits);
            foreach (var hit in hits)
            {
                Assert.True(hit.SpellDamage >= 0);
                var loss = 200 - hit.Monster!.HitPoints!.Value;
                Assert.Equal(Math.Max(1, hit.SpellDamage - (spell >= SpellId.FlameboltI ? armor : 0)), loss);
            }
        }
    }
    [Fact]
    public void ARound_SpellHitsPointAtTheirTakesLines_MeleeHasNone()
    {
        // What the I field plans from (GameSession.ResolveCombatRound: the round's log and TakeRoundHits).
        var (enc, mons) = Fight(SpellId.MindblastIv, 1);
        var fighter = new Character
        {
            Name = "FIGHTER", Class = CharacterClass.Fighter, Level = 4, Status = CharacterStatus.Ok,
            Stats = new CharacterStats(14, 16, 12, 13, 11, 9), Skills = new CharacterSkills(90, 30, 0, 0, 0, 0, 0, 0),
            HpCur = 80, HpMax = 80, WeaponDamageA = 12, WeaponDamageB = 8,
        };
        enc.Party.Members.Add(fighter);
        enc.Prepare();
        var rng = new SeededRandomSource(11);
        var spellHits = 0;
        var meleeHits = 0;
        for (var round = 0; round < 3; round++)
        {
            enc.EnsurePrep();
            enc.SetAction(0, CombatAction.CastSpell(SpellId.MindblastIv));
            enc.SetAction(1, CombatAction.Thrust(0));
            enc.TakeRoundHits();
            var result = new CombatResolver().ResolveRound(enc, rng);
            foreach (var hit in enc.TakeRoundHits())
            {
                if (hit.Source == DamageSource.PartySpell)
                {
                    spellHits++;
                    Assert.Equal(GameText.F("core.combat.x_takes_x_damage", hit.Monster!.Name, Math.Max(1, hit.SpellDamage)),
                        result.Log[hit.Line]);
                }
                else
                {
                    meleeHits += hit.Source == DamageSource.PartyMelee ? 1 : 0;
                    Assert.Equal(-1, hit.SpellDamage);
                }
            }
            if (result.EncounterOver)
                break;
        }
        Assert.True(spellHits >= mons.Length);
        Assert.True(meleeHits > 0);
    }

    // The golden traces (CombatTraceGoldenTests) hold the RNG stream: the tag reads values the spell already rolled.
}
