namespace OpenPhantasie.Core;

/// <summary>A character's combat rank (record <c>+0x08</c>): Back 0, Middle 1, Front 2; new characters start Front.</summary>
public enum P3PartyRank
{
    Back = 0,
    Middle = 1,
    Front = 2,
}

/// <summary>III's combat actions in menu order (strings 490–497; the action code is string 3 + this, R2 forms 11–16).</summary>
public enum P3Action
{
    Attack = 0,
    Lunge = 1,
    Slash = 2,
    Thrust = 3,
    FireBow = 4,
    Parry = 5,
    Aim = 6,
    CastSpell = 7,
}

/// <summary>The stored action and the monster rank (1 or 2) it goes at.</summary>
public readonly record struct P3ActionChoice(P3Action Action, int Rank = 1);

/// <summary>What the action menu needs to know about the member choosing.</summary>
/// <param name="Class">The member's class.</param>
/// <param name="AttackPercent">The Attack skill (record <c>+0x30</c>).</param>
/// <param name="Body">The member's body areas.</param>
/// <param name="Status">The member's status (Down bars the bow).</param>
/// <param name="HasBow">A bow is worn (<c>+0x86</c> ≠ 0).</param>
/// <param name="IsElemental">Race Elemental (16): no bow.</param>
/// <param name="RankTwoExists">The second monster group is present.</param>
public sealed record P3Chooser(
    CharacterClass Class, int AttackPercent, P3BodyAreas Body, P3Status Status, bool HasBow, bool IsElemental,
    bool RankTwoExists);

/// <summary>
/// Phantasie III's combat rules (<c>combat-actions-and-ranks.md</c>, <c>bows.md</c>): party ranks, the action menu with its
/// fallbacks, melee and bow chances, the running-sum damage, and monster melee against the party. Pure rules over small
/// inputs; the fight loop that calls them is the III campaign's.
/// </summary>
public static class P3Combat
{
    /// <summary>Attack % from which Attack gives two swings (below: Thrust).</summary>
    public const int AttackMinimum = 76;

    /// <summary>Attack % from which Slash is allowed (and, manual D3-013, Aim).</summary>
    public const int SlashMinimum = 150;

    /// <summary>D3-019 manual: the chance against a Down / Out target is doubled up to this cap.</summary>
    public const int DownTargetCap = 95;

    /// <summary>Melee penalty of a Down attacker.</summary>
    public const int DownAttackerPenalty = 40;

    /// <summary>Melee penalty against the second monster rank.</summary>
    public const int RankTwoPenalty = 15;

    /// <summary>Accuracy / Party Accuracy (#55 / #56): Fire Bow % = 250.</summary>
    public const int AccuracyPercent = 250;

    // ---- Ranks -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>30 · (own rank − average rank)</c>, the average being the integer <c>Σ rank / party size</c> (root <c>0xb934</c>).
    /// Added to the member's melee chance and to a monster's melee chance against the member; not used by bows, spells or
    /// breath.
    /// </summary>
    public static int RankTerm(P3PartyRank own, IReadOnlyList<P3PartyRank> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count == 0) return 0;
        var average = party.Sum(r => (int)r) / party.Count;
        return 30 * ((int)own - average);
    }

    // ---- The action menu ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The action a menu pick stores (root <c>0xfc41</c>), with the decided III rules:
    /// <list type="bullet">
    /// <item>Right Arm Broken or Gone → Parry, whatever was picked.</item>
    /// <item>Attack: Attack % ≥ 76 → Attack (two swings), else Thrust. A Thief picks the rank (manual, D3-012) or always
    /// rank 1 (<c>p3.thief-attack</c> = <c>dos</c>).</item>
    /// <item>Lunge: rank 2 present and a Fighter (manual, D3-011; Thieves too under <c>p3.lunge</c> = <c>dos</c>) → Lunge
    /// at rank 2, else Thrust.</item>
    /// <item>Slash: Attack % ≥ 150 → Slash (a Thief picks the rank), else an Attack (manual, D3-010, which itself becomes
    /// a Thrust below 76) or a Thrust (<c>p3.slash-fallback</c> = <c>dos</c>).</item>
    /// <item>Thrust: a Thief picks the rank when rank 2 exists, others rank 1.</item>
    /// <item>Fire Bow: any limb ≥ Broken, no bow, an elemental or Down → Thrust; else the rank when rank 2 exists.</item>
    /// <item>Aim: Attack % ≥ 150 → Aim at rank 1, else Thrust (manual, D3-013); DOS always Aims.</item>
    /// <item>Parry; Cast Spell (spell checks are the caster's, not decoded here).</item>
    /// </list>
    /// <paramref name="pickedRank"/> is the answer to `p3combat.x_target_which_rank` when the menu would ask (1 or 2). A Thrust that is
    /// a fallback goes at rank 1, as DOS stores it, except that under the manual rule a Thief's chosen rank carries through
    /// (the manual lets Thieves thrust, attack or slash at any rank; how a fallback keeps the rank is manual-silent).
    /// </summary>
    public static P3ActionChoice Choose(P3Action picked, P3Chooser who, int pickedRank, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(who);
        ArgumentNullException.ThrowIfNull(rules);
        if (who.Body.ForcesParry) return new(P3Action.Parry);
        var thief = who.Class == CharacterClass.Thief;
        var rank = who.RankTwoExists && pickedRank == 2 ? 2 : 1;
        var thiefRank = thief && who.RankTwoExists ? rank : 1;
        var carry = thief && !rules.P3ThiefAttackRankOne() ? thiefRank : 1; // rank a manual-rule Thief's fallback keeps

        P3ActionChoice Attack() => who.AttackPercent >= AttackMinimum
            ? new(P3Action.Attack, rules.P3ThiefAttackRankOne() ? 1 : thiefRank)
            : new(P3Action.Thrust, carry);

        switch (picked)
        {
            case P3Action.Attack:
                return Attack();
            case P3Action.Lunge:
                var mayLunge = who.Class == CharacterClass.Fighter || (thief && rules.P3ThiefLunge());
                return who.RankTwoExists && mayLunge ? new(P3Action.Lunge, 2) : new(P3Action.Thrust, carry);
            case P3Action.Slash:
                if (who.AttackPercent >= SlashMinimum) return new(P3Action.Slash, thiefRank);
                return rules.P3SlashToThrust() ? new(P3Action.Thrust, 1) : Attack();
            case P3Action.Thrust:
                return new(P3Action.Thrust, thiefRank);
            case P3Action.FireBow:
                if (who.Body.BlocksBow || !who.HasBow || who.IsElemental || who.Status == P3Status.Down)
                    return new(P3Action.Thrust, 1);
                return new(P3Action.FireBow, rank);
            case P3Action.Aim:
                if (!rules.P3DosAim() && who.AttackPercent < SlashMinimum) return new(P3Action.Thrust, 1);
                return new(P3Action.Aim, 1);
            case P3Action.Parry:
                return new(P3Action.Parry);
            case P3Action.CastSpell:
                return new(P3Action.CastSpell, rank);
            default:
                throw new ArgumentOutOfRangeException(nameof(picked), picked, null);
        }
    }

    /// <summary>
    /// The re-check of a stored action (root <c>0x10be4</c>, run from the party menu, and <c>0xc03b</c> in the attack itself): Right Arm ≥ Broken → Parry; Fire Bow
    /// with any limb ≥ Broken or while Down → Thrust at rank 1.
    /// </summary>
    public static P3ActionChoice Recheck(P3ActionChoice choice, P3BodyAreas body, P3Status status)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.ForcesParry) return new(P3Action.Parry);
        if (choice.Action == P3Action.FireBow && (body.BlocksBow || status == P3Status.Down))
            return new(P3Action.Thrust, 1);
        return choice;
    }

    // ---- Melee (character → monster) ---------------------------------------------------------------------------------

    /// <summary>Swings per action: Attack 2, Slash <c>Attack % / 75 + 1</c>, the rest 1.</summary>
    public static int Swings(P3Action action, int attackPercent) => action switch
    {
        P3Action.Attack => 2,
        P3Action.Slash => attackPercent / 75 + 1,
        _ => 1,
    };

    /// <summary>Damage modifier: Slash −1, Lunge and Thrust +1, the rest 0.</summary>
    public static int DamageModifier(P3Action action) => action switch
    {
        P3Action.Slash => -1,
        P3Action.Lunge or P3Action.Thrust => 1,
        _ => 0,
    };

    /// <summary>Hit-location row: Aim row 3, Fire Bow row 0, other melee row 2; the "tall monster" flag forces row 4.</summary>
    public static P3LocationRow LocationRow(P3Action action, bool tallMonster) =>
        tallMonster ? P3LocationRow.Tall
        : action switch { P3Action.Aim => P3LocationRow.Aim, P3Action.FireBow => P3LocationRow.Bow, _ => P3LocationRow.Melee };

    /// <summary>
    /// The base chance of one swing: <c>Attack % / swings</c>; an aimed blow uses <c>Attack % / 2</c> (manual, D3-013, as one
    /// Attack swing) or the full Attack % (<c>p3.aim</c> = <c>dos</c>).
    /// </summary>
    public static int SwingBase(P3Action action, int attackPercent, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (action == P3Action.Aim) return rules.P3DosAim() ? attackPercent : attackPercent / 2;
        return attackPercent / Swings(action, attackPercent);
    }

    /// <summary>
    /// The chance against a target that is not Okay (D3-019): Asleep always 100. Down and Out (Unconscious): DOS 100
    /// (<c>p3.down-targets</c> = <c>dos</c>), manual double the normal chance capped at 95, never below the normal chance.
    /// A negative normal chance counts as 0, so a target that could not be hit while Okay still can't be hit while Down
    /// (open point for the user: the manual's "much easier to hit" gives no rule for it). Okay targets keep
    /// <paramref name="normal"/>.
    /// </summary>
    public static int TargetChance(int normal, P3Status target, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return target switch
        {
            P3Status.Asleep => 100,
            P3Status.Down or P3Status.Out when rules.P3DownAlwaysHit() => 100,
            P3Status.Down or P3Status.Out => Math.Max(normal, Math.Min(DownTargetCap, 2 * Math.Max(0, normal))),
            _ => normal,
        };
    }

    /// <summary>
    /// One melee swing's chance (root <c>0xc03b</c>): <c>base − monster defence</c>, −40 for a Down attacker, + the rank term,
    /// −15 against rank 2, then <c>max(1, …)</c> and the target-status rule (<see cref="TargetChance"/>; DOS's order puts
    /// the 100 before the <c>max</c>, which gives the same result).
    /// </summary>
    public static int MeleeChance(
        int swingBase, int monsterDefence, bool attackerDown, int rankTerm, int targetRank, P3Status target, GameOptions rules)
    {
        var c = swingBase - monsterDefence;
        if (attackerDown) c -= DownAttackerPenalty;
        c += rankTerm;
        if (targetRank == 2) c -= RankTwoPenalty;
        return TargetChance(Math.Max(1, c), target, rules);
    }

    /// <summary>The weapon die: the weapon's points, bare hands <c>Attack % / 25</c>; at least 1 (root <c>0xd8a6</c>).</summary>
    public static int WeaponDie(int weaponPoints, int attackPercent) =>
        Math.Max(1, weaponPoints > 0 ? weaponPoints : attackPercent / 25);

    /// <summary>
    /// A melee action's swings (root <c>0xc03b</c>). RNG order per swing: the target pick (the caller's
    /// <paramref name="pickTarget"/>), the weapon die, the hit roll <c>rnd(100) ≤ chance</c>. The damage is a <b>running
    /// sum</b>: it starts at the action's modifier and every swing adds its die, hit or miss, so later swings hit harder
    /// (verified live s81). A hit deals <c>max(1, sum)</c> through <paramref name="applyHit"/>, which applies HP and injury
    /// and so can change the next swing's target status; <paramref name="miss"/> hears each miss in turn. Returns the swings'
    /// results.
    /// </summary>
    public static IReadOnlyList<P3Swing> ResolveMelee(
        P3MeleeAttack attack, Func<IRandomSource, int?> pickTarget, Func<int, P3Status> statusOf,
        Action<int, int, P3LocationRow> applyHit, IRandomSource rng, GameOptions rules, Action<int>? miss = null)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(pickTarget);
        ArgumentNullException.ThrowIfNull(statusOf);
        ArgumentNullException.ThrowIfNull(applyHit);
        var n = Swings(attack.Action, attack.AttackPercent);
        var swingBase = SwingBase(attack.Action, attack.AttackPercent, rules);
        var row = LocationRow(attack.Action, attack.TallMonster);
        var die = WeaponDie(attack.WeaponPoints, attack.AttackPercent);
        var sum = DamageModifier(attack.Action);
        var swings = new List<P3Swing>(n);
        for (var i = 0; i < n; i++)
        {
            if (pickTarget(rng) is not { } t)
                break;
            sum += P3Dice.Rnd(rng, die);
            var chance = MeleeChance(swingBase, attack.MonsterDefence, attack.AttackerDown, attack.RankTerm,
                attack.TargetRank, statusOf(t), rules);
            var hit = P3Dice.Rnd(rng, 100) <= chance;
            var damage = hit ? Math.Max(1, sum) : 0;
            if (hit) applyHit(t, damage, row);
            else miss?.Invoke(t);
            swings.Add(new P3Swing(t, chance, hit, damage));
        }
        return swings;
    }

    // ---- Bows ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A bow is usable when <c>STR + DEX ≥ requirement</c> (ovl2 <c>0x39b5</c>; shields and armour STR, melee weapons
    /// <c>2·STR + DEX</c>).
    /// </summary>
    public static bool CanUseBow(CharacterStats stats, int requirement)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return stats.Str + stats.Dex >= requirement;
    }

    /// <summary>
    /// The Fire Bow skill's training seed (<c>+0x4a</c>, root <c>0xa1f2</c>): <c>2·Level + 4·DEX + STR + 10</c>, +30 Thief,
    /// +15 Elf.
    /// </summary>
    public static int FireBowSeed(int level, CharacterStats stats, CharacterClass cls, Race race)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return 2 * level + 4 * stats.Dex + stats.Str + 10
               + (cls == CharacterClass.Thief ? 30 : 0) + (race == Race.Elf ? 15 : 0);
    }

    /// <summary>
    /// The per-round bow values (root <c>0xd730</c>, spells root <c>0xc3cc</c> / <c>0xc687</c>): Fire Bow % and the damage
    /// range. Plain: skill and <c>2·points + 4</c> (no bow: 4; at least 1). Arrow Flame I / II: skill + 50 / + 100 and, manual
    /// (D3-017), points + 15 / + 25 fed into <c>2·points + 4</c>; under <c>p3.arrow-flame</c> = <c>dos</c> the range is
    /// replaced by <c>points + 15 / 25</c>. Accuracy: 250 %, range unchanged. A spell's values last the rest of the fight
    /// (DOSBox s82, D3-017: the damage words stayed 16 / 35 in round 2 with no recast; <c>03c-phantasie3-re.md</c> 3c.8):
    /// the caller keeps them for the fight.
    /// </summary>
    public static P3BowStats BowStats(int bowPoints, int fireBowSkill, P3BowSpell spell, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        static int Range(int points) => Math.Max(1, 2 * points + 4);
        var plain = bowPoints > 0 ? Range(bowPoints) : 4;
        return spell switch
        {
            P3BowSpell.ArrowFlameI => new(fireBowSkill + 50,
                rules.P3DosArrowFlame() ? Math.Max(1, bowPoints + 15) : Range(bowPoints + 15)),
            P3BowSpell.ArrowFlameII => new(fireBowSkill + 100,
                rules.P3DosArrowFlame() ? Math.Max(1, bowPoints + 25) : Range(bowPoints + 25)),
            P3BowSpell.Accuracy => new(AccuracyPercent, plain),
            _ => new(fireBowSkill, plain),
        };
    }

    /// <summary>
    /// A bow shot's chance (root <c>0xc03b</c>): <c>bow % − monster defence / 2</c>, no rank term and no rank-2 penalty
    /// (party position does not affect bows), then <c>max(1, …)</c> and the target-status rule.
    /// </summary>
    public static int BowChance(int bowPercent, int monsterDefence, P3Status target, GameOptions rules) =>
        TargetChance(Math.Max(1, bowPercent - monsterDefence / 2), target, rules);

    /// <summary>One bow shot: <c>rnd(100) ≤ chance</c>, then damage <c>max(1, rnd(range))</c>; 0 on a miss.</summary>
    public static int FireBow(P3BowStats bow, int monsterDefence, P3Status target, IRandomSource rng, GameOptions rules)
    {
        var chance = BowChance(bow.Percent, monsterDefence, target, rules);
        return P3Dice.Rnd(rng, 100) <= chance ? Math.Max(1, P3Dice.Rnd(rng, bow.DamageRange)) : 0;
    }

    // ---- Monster melee (monster → character) ---------------------------------------------------------------------------

    /// <summary>
    /// A character's defence against monster melee (root <c>0xd730</c>): <c>shield / 2 + Parry / 4 + 2·DEX</c> and the
    /// armour term <c>+ 5·armour</c> (manual, D3-018: armour protects) or DOS's <c>− 5·armour</c> (<c>p3.armour-defence</c> =
    /// <c>dos</c>, verified live s81). Points are the worn items' point values.
    /// </summary>
    public static int Defence(int shieldPoints, int armourPoints, int parry, int dex, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var armour = 5 * armourPoints;
        return shieldPoints / 2 + (rules.P3DosArmourSign() ? -armour : armour) + parry / 4 + 2 * dex;
    }

    /// <summary>Attacks a monster makes per round: <c>max(1, (attack + 80) / 115)</c> (attack = group word <c>+0x22</c>).</summary>
    public static int MonsterAttacks(int attack) => Math.Max(1, (attack + 80) / 115);

    /// <summary>
    /// A monster's chance against one character (root <c>0xb64e</c>): <c>attack / n − defence</c>, −40 when the monster
    /// group's member 0 is Down, + the character's rank term, − the shield's points when the character Parries, then the
    /// target-status rule. No lower clamp (a negative chance never hits).
    /// </summary>
    public static int MonsterChance(
        int attack, int defence, bool monsterDown, int rankTerm, bool parrying, int shieldPoints, P3Status target,
        GameOptions rules)
    {
        var c = attack / MonsterAttacks(attack) - defence;
        if (monsterDown) c -= DownAttackerPenalty;
        c += rankTerm;
        if (parrying) c -= shieldPoints;
        return TargetChance(c, target, rules);
    }
}

/// <summary>What a melee action needs (<see cref="P3Combat.ResolveMelee"/>).</summary>
public sealed record P3MeleeAttack(
    P3Action Action, int AttackPercent, int WeaponPoints, int MonsterDefence, bool AttackerDown, int RankTerm,
    int TargetRank, bool TallMonster);

/// <summary>One swing: the target slot, its chance, hit or miss, the damage dealt (0 on a miss).</summary>
public readonly record struct P3Swing(int Target, int Chance, bool Hit, int Damage);

/// <summary>A spell in effect on a member's bow fire.</summary>
public enum P3BowSpell
{
    None,
    ArrowFlameI,
    ArrowFlameII,
    Accuracy,
}

/// <summary>Fire Bow % and the damage range <c>rnd(DamageRange)</c> for a round.</summary>
public readonly record struct P3BowStats(int Percent, int DamageRange);
