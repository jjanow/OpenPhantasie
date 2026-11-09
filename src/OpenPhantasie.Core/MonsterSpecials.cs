namespace OpenPhantasie.Core;

/// <summary>Extra effect a melee special (ids 34/35) adds to every hit (OV2 <c>0xf49</c>).</summary>
public enum MonsterMeleeRider
{
    None,

    /// <summary>Id 34 → 100: target SLEEPs and loses its action, "A STUNNING BLOW".</summary>
    StunningBlow,

    /// <summary>Id 35 → 101: <c>@18 MP cur := (6·MP + 5)/10</c>, `core.combat.magic_drained`.</summary>
    MagicDrain,
}

/// <summary>
/// Monster special abilities — OV2 <c>0x1004</c> → <c>0x16a9</c> (<c>combat-resolution.md</c> §Monster specials, s26).
/// </summary>
public static class MonsterSpecials
{
    /// <summary>Gate (<c>0xd30</c>): special if <c>rand + 1 ≤ gate·(2g + 2)/(ranks + 2)</c>; g = rank position 0..2.</summary>
    public static bool GatePasses(int roll0To99, int gate, int rankPosition, int rankCount) =>
        roll0To99 + 1 <= gate * (2 * rankPosition + 2) / (rankCount + 2);

    /// <summary>
    /// Picks one of the monster's three ids (<c>rand·3/100</c>) and applies it. Returns the melee rider when the id
    /// falls through to melee (34/35 with a rider, breath 36/37 without), or null when the special used up the turn.
    /// </summary>
    public static MonsterMeleeRider? Use(
        CombatEncounter encounter, MonsterRank group, Monster mon, IRandomSource rng, List<string> log)
    {
        var id = mon.Specials[Roll(rng) * 3 / 100];
        encounter.TagLine(log, null, mon); // presentation only (I Amiga: the caster's flash)
        log.Add(GameText.F(id is 36 or 37 ? "core.combat.x_breathes" : "core.combat.x_casts_a_spell", mon.Name));

        switch (id)
        {
            case >= 1 and <= 4:
                SingleTarget(encounter, id, rng, log);
                return null;
            case >= 5 and <= 8:
                var n = id - 5;
                foreach (var m in group.Members) m.AttackPower = 30 * n * n + 35;
                return null;
            case >= 9 and <= 12:
                n = id - 8;
                foreach (var m in group.Members) m.DamageField = 15 * n * n / 10 + 8;
                return null;
            case >= 13 and <= 16:
                foreach (var m in group.Members) m.Armor = 4 * (id - 12) + 4;
                return null;
            case >= 17 and <= 20:
                // DOS scales weapon damage @248 (field 0x39) under this message; the remaster lowers armor @246 to
                // match it (D-016, user s26). The p1.armor-reduced option's DOS value scales @248 as DOS does.
                log.Add(GameText.S("core.combat.armor_reduced"));
                PartyWide(encounter, mon, 10 - 2 * (id - 16), PartyEffect.ArmorReduced, rng, log);
                return null;
            case >= 21 and <= 24:
                log.Add(GameText.S("core.combat.party_slowed"));
                PartyWide(encounter, mon, 10 - 2 * (id - 20), PartyEffect.Slowed, rng, log);
                return null;
            case >= 25 and <= 28:
                n = id - 25;
                PartyWide(encounter, mon, 100 * n * n + 60, PartyEffect.Blast, rng, log);
                return null;
            case >= 29 and <= 32:
                n = id - 29;
                PartyWide(encounter, mon, 80 * n * n + 40, PartyEffect.ArmoredBlast, rng, log);
                return null;
            case 33:
                PartyWide(encounter, mon, 0, PartyEffect.Sleep, rng, log);
                return null;
            case 34:
                return MonsterMeleeRider.StunningBlow;
            case 35:
                return MonsterMeleeRider.MagicDrain;
            case 36:
            case 37:
                // Breath keeps its id (> 0x21), so the caller goes on to a normal melee (0x1074 → 0xd68).
                PartyWide(encounter, mon, id == 36 ? 200 : 360, PartyEffect.ArmoredBlast, rng, log);
                return MonsterMeleeRider.None;
            default:
                // Not in the data; DOS has no case for ids > 37 and melees.
                return MonsterMeleeRider.None;
        }
    }

    /// <summary>
    /// Rider after a melee hit (OV2 <c>0xf49</c>), once the damage is applied and the party is not wiped.
    /// <paramref name="stunTheDead"/> = DOS (D-015, <c>p1.stunning-blow</c> = <c>dos</c>): a STUNNING BLOW also puts a member
    /// the hit just killed to SLEEP at HP 0; that member counts as alive again, never wakes (waking needs HP 1) and can die
    /// again from the next hit.
    /// </summary>
    public static void ApplyRider(
        MonsterMeleeRider rider, Character target, CombatantPrep prep, List<string> log, bool stunTheDead = false)
    {
        switch (rider)
        {
            case MonsterMeleeRider.MagicDrain:
                target.MagicCur = (6 * target.MagicCur + 5) / 10;
                log.Add(GameText.S("core.combat.magic_drained"));
                break;
            case MonsterMeleeRider.StunningBlow:
                // DOS sets SLEEP even when the hit killed the target; the remaster leaves the dead dead (D-015, user s26)
                // unless the save plays DOS's rule.
                if (target.Status == CharacterStatus.Dead && !stunTheDead)
                    break;
                target.Status = CharacterStatus.Sleep;
                prep.PackedAction = CombatAction.Cleared;
                log.Add(GameText.S("core.combat.a_stunning_blow"));
                break;
        }
    }

    private enum PartyEffect { Blast, ArmoredBlast, Sleep, ArmorReduced, Slowed }

    /// <summary>Ids 1–4: one non-DEAD member, <c>rand·k/100</c> damage, no resist, no armor (<c>0x1731</c>).</summary>
    private static void SingleTarget(CombatEncounter encounter, int id, IRandomSource rng, List<string> log)
    {
        var k = id switch { 1 => 12, 2 => 35, 3 => 70, _ => 120 };
        if (encounter.Rules.IsDos(RuleOptionRegistry.MonsterSpellTarget))
        {
            // D-033 dos (p1.monster-spell-target): DOS reads list[rand·[0x6490]/100] as they stand (0x1731..0x1742).
            // Before any monster turn this round the word is the last living index, so the last listed member can't be picked; after
            // a pass-3/5 summon (or a second summon) under p1.summon-party dos, entry 5 can be a stale slot (DEAD or listed
            // twice). A manual summon leaves the list alone: the elemental is listed at the next rebuild. A DEAD target
            // takes nothing and gets no line (0x124a returns), but the damage is still rolled.
            var party = encounter.Party;
            var slot = party.DosLiving.Entry(Roll(rng) * party.DosLiving.Word / 100);
            var damage = Roll(rng) * k / 100;
            // [?] an entry never written (start-up memory) or a slot past the party: no target.
            if (slot is { } s && s < party.Members.Count)
                HitMember(encounter, party.Members[s], damage, rng, log);
            return;
        }
        var living = encounter.Party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList();
        var target = living[Roll(rng) * living.Count / 100];
        HitMember(encounter, target, Roll(rng) * k / 100, rng, log);
    }

    /// <summary>
    /// <c>0x1a7b(power, code, group)</c>: every party slot in order (DEAD included) draws <c>scaled = rand·power/100</c>;
    /// all codes but the armored blast then roll to resist with <c>(group CV − @16 MP max)·5 + 50 &gt; rand</c>.
    /// </summary>
    private static void PartyWide(
        CombatEncounter encounter, Monster caster, int power, PartyEffect effect, IRandomSource rng, List<string> log)
    {
        var members = encounter.Party.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var ch = members[i];
            var prep = encounter.Prep[i];
            var scaled = Roll(rng) * power / 100;

            if (effect == PartyEffect.ArmoredBlast)
            {
                HitMember(encounter, ch, scaled / 10 - prep.ArmorEff, rng, log);
                continue;
            }

            var chance = CombatMath.CvHitChance(caster.CombatRating, ch.MagicMax);
            if (!CombatMath.HitsStrict(Roll(rng), chance))
                continue;

            switch (effect)
            {
                case PartyEffect.Blast:
                    HitMember(encounter, ch, scaled / 10, rng, log);
                    break;
                case PartyEffect.Sleep:
                    if (ch.Status != CharacterStatus.Dead)
                        ch.Status = CharacterStatus.Sleep;
                    log.Add(GameText.F("core.combat.x_is_slept", ch.Name)); // DOS prints it for DEAD members too
                    prep.PackedAction = CombatAction.Cleared;
                    break;
                case PartyEffect.ArmorReduced when encounter.Rules.DosArmorReduced:
                    prep.WeaponDamageEff = (prep.WeaponDamageEff * scaled + 5) / 10; // DOS field 0x39 = @248 (D-016)
                    break;
                case PartyEffect.ArmorReduced:
                    prep.ArmorEff = (prep.ArmorEff * scaled + 5) / 10;
                    break;
                case PartyEffect.Slowed:
                    prep.WeaponHitEff = (prep.WeaponHitEff * scaled + 5) / 10;
                    break;
            }
        }
    }

    /// <summary><c>0x124a(t, d, 0)</c>: DEAD members are skipped; damage floored to 1. Runs the damage modules.</summary>
    private static void HitMember(
        CombatEncounter encounter, Character target, int damage, IRandomSource rng, List<string> log)
    {
        if (target.Status == CharacterStatus.Dead)
            return;
        CombatResolver.ApplyPartyDamage(
            encounter, target, CombatMath.ApplyDamageFloor(damage), DamageSource.MonsterSpecial, rng, log);
    }

    private static int Roll(IRandomSource rng) => rng.Next(0, 100);
}
