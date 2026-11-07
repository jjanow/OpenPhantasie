namespace OpenPhantasie.Core;

/// <summary>
/// <c>[0x74b0]</c>: a spell has ended the fight. In DOS combat stops at the next round top (OV2 <c>0x3b84</c>, before the
/// morale check) and the round in which the spell was cast still plays out. The remaster keeps that for
/// <see cref="Ended"/>; <see cref="Teleported"/> and <see cref="Charmed"/> stop the round right after the caster (D-022, D-023).
/// </summary>
public enum SpellCombatEnd
{
    None = 0,

    /// <summary>TELEPORTATION (<c>0x7da</c>): no gold, no treasure; kill XP is kept.</summary>
    Teleported = 1,

    /// <summary>Dissolve (<c>0x869</c>), DISPELL UNDEAD's run (<c>0x999</c>) or FEAR's run (D-021): nothing extra at the end.</summary>
    Ended = 2,

    /// <summary>CHARM (D-022, remaster only): ends like a successful GREETINGS — kill XP kept, no gold, no treasure.</summary>
    Charmed = 3,

    /// <summary>
    /// TELEPORTATION as DOS (<c>p1.teleport</c> = <c>dos</c>, D-023): <c>[0x74b0]</c> = 1, the round plays out and combat
    /// ends at the next round top; then as <see cref="Teleported"/> (no gold, no treasure, kill XP kept).
    /// </summary>
    TeleportedAtRoundTop = 4,
}

/// <summary>
/// How a summoned elemental gets its combat fields. DOS never preps party slot 7 (a loop-bound bug), so it fights with
/// 0 weapon hit / damage / armor and can't hit a monster (unless a party buff follows the summon); the manual says it is
/// called up "to fight for the party" (PDF p.20). D-032 (user, s34): fix the bug — <see cref="Prepared"/> is the default.
/// </summary>
public enum ElementalPrepRule
{
    /// <summary>DOS: stored record values (0 for a fresh template), as OV2 <c>0x3263</c> never reaches slot 7.</summary>
    Dos = 0,

    /// <summary>The normal fight-start prep from the elemental's record (attack, weapon points, armor, defense).</summary>
    Prepared = 1,
}

/// <summary>Spell ends that stop the round right after the caster acts, rather than at the next round top.</summary>
public static class SpellCombatEndExtensions
{
    public static bool EndsAtOnce(this SpellCombatEnd end) => end is SpellCombatEnd.Teleported or SpellCombatEnd.Charmed;

    /// <summary>Spell ends that pay no gold and forfeit the treasure (<c>0x3f77</c>): TELEPORTATION (either timing) and CHARM.</summary>
    public static bool PaysNoGold(this SpellCombatEnd end) =>
        end is SpellCombatEnd.Teleported or SpellCombatEnd.Charmed or SpellCombatEnd.TeleportedAtRoundTop;
}

/// <summary>
/// Combat effects of the special spells CHARM … AWAKEN (cast_id 37–47), OV2 jump table <c>0x9d1</c>
/// (<c>combat-resolution.md</c> §Combat spell effects, s31). <c>lvl</c> = caster level <c>@88</c>,
/// <c>L</c> = encounter level <see cref="CombatEncounter.MonsterLevel"/>, rolls 0..99 in DOS draw order.
/// SLEEP, AWAKEN and RESURRECTION stay in <see cref="SpellCaster"/> (SLEEP/AWAKEN: status-effect pass).
/// CHARM, TELEPORTATION, FEAR and DISPELL UNDEAD follow the manual by user decision (D-021…D-024); the DOS rules are kept
/// in <c>combat-resolution.md</c>.
/// </summary>
public static class CombatSpells
{
    /// <summary>FEAR: encounter level drop per successful cast (<c>0x834</c>).</summary>
    public const int FearLevelDrop = 6;

    /// <summary>NINJA I / II values written to the caster's prep (<c>0x9a1</c> / <c>0x7ed</c>).</summary>
    public static readonly NinjaForm NinjaI = new(WeaponHitEff: 200, WeaponDamageEff: 20, ArmorEff: 8);
    public static readonly NinjaForm NinjaIi = new(WeaponHitEff: 255, WeaponDamageEff: 25, ArmorEff: 10);

    public readonly record struct NinjaForm(int WeaponHitEff, int WeaponDamageEff, int ArmorEff);

    /// <summary>
    /// CHARM: the DOS roll (<c>0x767</c>: <c>r₁ = rand, r₂ = rand</c>; works if <c>r₁·lvl &gt; r₂·L</c>). On success the
    /// fight ends at once like a successful GREETINGS (D-022, manual "they forget they are in combat"): kill XP kept,
    /// no gold, no treasure. DOS instead adds 15 to Σreaction and the monsters keep fighting.
    /// </summary>
    public static bool Charm(CombatEncounter encounter, Character caster, IRandomSource rng, List<string> log)
    {
        var casterSide = Roll(rng) * caster.Level;
        var monsterSide = Roll(rng) * encounter.MonsterLevel;
        if (casterSide <= monsterSide)
            return false;
        log.Add(GameText.S("core.combat.monsters_are_charmed"));
        if (encounter.Rules.IsDos(RuleOptionRegistry.Charm))
        {
            // D-022 dos (p1.charm, 0x797): Σreaction += 15 (stacks) and GREETINGS works again ([0x74b6] := 0).
            encounter.ReactionSum += CharmReactionBonus;
            encounter.MonsterDamaged = false;
            return true;
        }
        encounter.SpellEnd = SpellCombatEnd.Charmed;
        return true;
    }

    /// <summary>
    /// TELEPORTATION: always works and the party leaves at once (D-023, manual "safely away"). No roll, no message;
    /// kill XP kept, no gold, no treasure. DOS: <c>rand &lt; 3·lvl + 50</c> (<c>0x7b8</c>) and the round plays out.
    /// </summary>
    public static void Teleport(CombatEncounter encounter) => encounter.SpellEnd = SpellCombatEnd.Teleported;

    /// <summary>
    /// TELEPORTATION as DOS (<c>p1.teleport</c> = <c>dos</c>, D-023, OV2 <c>0x7b8</c>): works on <c>rand &lt; 3·lvl + 50</c>
    /// (no message either way); then the round plays out and combat ends at the next round top
    /// (<see cref="SpellCombatEnd.TeleportedAtRoundTop"/>). Returns whether it worked.
    /// </summary>
    public static bool TeleportDos(CombatEncounter encounter, Character caster, IRandomSource rng)
    {
        if (Roll(rng) >= 3 * caster.Level + 50)
            return false;
        encounter.SpellEnd = SpellCombatEnd.TeleportedAtRoundTop;
        return true;
    }

    /// <summary>CHARM's reaction bonus under DOS (<c>0x797</c>, D-022 <c>dos</c>).</summary>
    public const int CharmReactionBonus = 15;

    /// <summary>
    /// FEAR: the DOS roll and level drop (<c>0x80c</c>: if <c>rand &lt; 5·(lvl − L) + 50</c>, <c>L −= 6</c>, may go below 0),
    /// then — remaster, D-021, manual "running away in terror" — "MONSTERS RUN" by DISPELL UNDEAD's run path, and combat ends
    /// at the next round top with full gold (D-019). If any living monster <see cref="Monster.NeverFlees"/> (the Black Lord),
    /// only <c>L</c> drops, which keeps the FEAR + DISSOLVE win (II: Pluto and his beasts, unless <c>p2.fear-bosses</c> = <c>flee</c>).
    /// Remaster (user, s34): the runners don't act again this round. DISPELL's hold alone ends after pass 1 of round 1
    /// (<c>0x10f3</c>), so FEAR also marks every living monster done for the round (state 2, cleared at the round top).
    /// </summary>
    public static bool Fear(CombatEncounter encounter, Character caster, IRandomSource rng, List<string> log)
    {
        if (Roll(rng) >= 5 * (caster.Level - encounter.MonsterLevel) + 50)
            return false;
        encounter.MonsterLevel -= FearLevelDrop;
        // D-021 dos (p1.fear) / D2-023 st (p2.fear): the original only lowers L (no run); the manual makes the monsters run.
        if (encounter.Rules.OriginalFear
            || (!encounter.Rules.FearBossesFlee && encounter.AllMonsters.Any(m => m.IsAlive && m.NeverFlees)))
            return true;
        log.Add(GameText.S("core.combat.monsters_run"));
        encounter.MonstersFrozen = true;
        foreach (var mon in encounter.AllMonsters.Where(m => m.IsAlive))
            mon.DoneThisRound = true;
        encounter.SpellEnd = SpellCombatEnd.Ended;
        return true;
    }

    /// <summary>DISSOLVE (<c>0x83c</c>): if <c>rand ≤ 110 − 10·L</c>, <see cref="Dissolve"/>. No message on a miss.</summary>
    public static bool CastDissolve(CombatEncounter encounter, IRandomSource rng, List<string> log)
    {
        if (Roll(rng) > 110 - 10 * encounter.MonsterLevel)
            return false;
        Dissolve(encounter, log);
        return true;
    }

    /// <summary>
    /// Dissolve (<c>0x85b</c>, DISSOLVE and DISPELL UNDEAD): combat ends at the next round top as if every monster died,
    /// gold := 0, and the front-rank group pays <c>XP × [0x74be + 2g]</c> (<c>0x8c1</c>) — its last living <i>index</i>,
    /// so <c>(living − 1)</c> monsters' XP (−1 if it is already wiped). Other groups pay nothing extra.
    /// </summary>
    public static void Dissolve(CombatEncounter encounter, List<string> log)
    {
        log.Add(GameText.S("core.combat.monsters_start_dissolving"));
        encounter.SpellEnd = SpellCombatEnd.Ended;
        encounter.RewardGold = 0;
        encounter.Dissolved = true;
        if (encounter.Ranks.Count == 0 || encounter.Ranks[0].Members.Count == 0)
            return;
        var front = encounter.Ranks[0];
        var lastLivingIndex = front.Living.Count() - 1;
        encounter.RewardXp += front.Members[0].ExperienceValue * lastLivingIndex;
    }

    /// <summary>
    /// "Undead" for DISPELL UNDEAD: the data flag (D-024 / D2-022, manual; <c>monster-traits.json</c>). DOS <c>0x92e</c> and
    /// the ST's <c>0x842c</c> test per-monster XP % 7 == 0 (<paramref name="dos"/>).
    /// </summary>
    public static bool IsUndead(Monster monster, bool dos = false) =>
        dos ? monster.ExperienceValue % 7 == 0 : monster.Undead;

    /// <summary>
    /// DISPELL UNDEAD (<c>0x8fc</c>), front-rank group only. Nothing if it already worked on undead this fight
    /// (<c>[0x74b8]</c>) or the group isn't undead (no roll then, so it can be retried). Else, with
    /// <c>v = 5·(lvl − group level) − rand</c>: <c>v &gt; −5</c> dissolve; <c>−45 &lt; v ≤ −5</c> "MONSTERS RUN" — ready monsters
    /// don't act for the rest of the round and combat ends at the next round top with full gold; else nothing.
    /// </summary>
    public static void DispellUndead(CombatEncounter encounter, Character caster, IRandomSource rng, List<string> log)
    {
        if (encounter.DispellUndeadTried || encounter.Ranks.Count == 0 || encounter.Ranks[0].Members.Count == 0)
            return;
        var group = encounter.Ranks[0].Members[0];
        if (!IsUndead(group, encounter.Rules.OriginalDispellUndead))
            return;

        encounter.DispellUndeadTried = true;
        var v = 5 * (caster.Level - group.Level) - Roll(rng);
        if (v > -5)
        {
            Dissolve(encounter, log);
        }
        else if (v > -45)
        {
            log.Add(GameText.S("core.combat.monsters_run"));
            encounter.MonstersFrozen = true;      // [0x64a0] := 1
            encounter.SpellEnd = SpellCombatEnd.Ended;
        }
    }

    /// <summary>NINJA I / II (<c>0x9a1</c> / <c>0x7ed</c>): <b>sets</b> the caster's weapon hit / damage / armor eff.</summary>
    public static void Ninja(CombatantPrep prep, NinjaForm form)
    {
        prep.WeaponHitEff = form.WeaponHitEff;
        prep.WeaponDamageEff = form.WeaponDamageEff;
        prep.ArmorEff = form.ArmorEff;
    }

    /// <summary>
    /// SUMMON ELEMENTAL (OV2 <c>0x8cc</c>, <c>combat-resolution.md</c> §SUMMON ELEMENTAL): no roll and no message; the
    /// caller spends the MP either way. Works when an elemental was loaded at the Guild (<see cref="Party.Elemental"/>,
    /// <c>[0x62a8] ≠ 0</c>), it isn't already fighting, and the party has six non-DEAD members (SLEEP counts). It then
    /// joins as member 7 with no action this round.
    /// <para>D-033 (user, s34: manual "a party of six adventurers", PDF p.20): six non-DEAD members in every pass. DOS tests
    /// <c>[0x6490] == 5</c>, which means six living members until a monster has acted in the round and five after that
    /// (the variable switches from last index to count) — not reproduced. III (<see cref="ElementalRules.MinMembers"/> 1,
    /// <c>elementals.md</c> §3): no member count at all.</para>
    /// </summary>
    public static bool SummonElemental(CombatEncounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        var party = encounter.Party;
        if (party.Elemental is not { } elemental || party.ElementalSummoned)
            return false;
        var nonDead = party.Members.Count(m => m.Status != CharacterStatus.Dead);
        if (encounter.Rules.IsDos(RuleOptionRegistry.SummonParty))
        {
            // D-033 dos (p1.summon-party): [0x6490] == 5, the last living index after orders (six non-DEAD) but the
            // living count once a monster has acted this round (five non-DEAD).
            if ((encounter.LivingWordIsCount ? nonDead : nonDead - 1) != Party.MaxMembers - 1)
                return false;
        }
        else if (nonDead < party.Rules.ElementalRulesOrDefault.MinMembers)
            return false; // I: six non-DEAD (D-033); III (root 0xc912): no member count, one living caster is enough
        encounter.AddSummonedMember(elemental);
        return true;
    }

    /// <summary>
    /// A summoned elemental's combat fields. <see cref="ElementalPrepRule.Dos"/>: DOS prep (<c>0x3263</c>) only runs over
    /// party size 6, so slot 7 keeps its stored <c>@246/@248/@250</c> — 0 in every template — and defense <c>@12</c>;
    /// PARRY still reads the shield <c>@242</c>. <see cref="ElementalPrepRule.Prepared"/>: the normal prep from its record.
    /// (DOS buffs cast after the summon also persist on slot 7 for the trip; Core rebuilds per summon — not modeled.)
    /// </summary>
    public static CombatantPrep ElementalPrep(Character elemental, ElementalPrepRule rule)
    {
        ArgumentNullException.ThrowIfNull(elemental);
        if (rule == ElementalPrepRule.Prepared)
            return CombatEncounter.PrepFor(elemental);
        return new CombatantPrep
        {
            ArmorEff = 0,
            WeaponDamageEff = 0,
            WeaponHitEff = 0,
            ShieldSoak = elemental.ShieldSoak,
            DefenseRating = elemental.Word12,
            PackedAction = CombatAction.Cleared,
        };
    }

    /// <summary>
    /// End of combat after a spell set <see cref="CombatEncounter.SpellEnd"/> (<c>0x3f29</c>): kill XP always paid;
    /// TELEPORTATION and CHARM pay no gold and forfeit the treasure; dissolve prints "MONSTERS KILLED" (<c>[0x74aa]</c>).
    /// <paramref name="log"/> continues a round's log when the spell ends the round at once.
    /// </summary>
    public static CombatResult EndBySpell(CombatEncounter encounter, List<string>? log = null)
    {
        log ??= new List<string>();
        var gold = GoldAtEnd(encounter);
        if (encounter.SpellEnd.PaysNoGold())
            encounter.TreasureForfeited = true;
        if (encounter.Dissolved || encounter.AllMonsters.All(m => !m.IsAlive))
            log.Add(GameText.S("core.combat.monsters_killed"));
        if (!encounter.Rules.SurvivorShares) // D-053 dos: DOS prints only the shares
            log.Add(GameText.F("core.combat.x_xp_and_x_gp", encounter.RewardXp, gold));
        return new CombatResult
        {
            EncounterOver = true,
            PartyWon = encounter.Dissolved,
            RewardXp = encounter.RewardXp,
            RewardGold = gold,
            Log = log,
        };
    }

    /// <summary>Gold paid at the end of combat: 0 after TELEPORTATION (<c>0x3f77</c>) or CHARM, else the encounter's purse.</summary>
    public static long GoldAtEnd(CombatEncounter encounter) =>
        encounter.SpellEnd.PaysNoGold() ? 0 : encounter.RewardGold;

    private static int Roll(IRandomSource rng) => rng.Next(0, 100);
}
